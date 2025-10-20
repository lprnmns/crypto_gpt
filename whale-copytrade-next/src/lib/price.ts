import { getAddress, type Address } from "viem";
import { chainlinkAbi } from "@/abi/chainlink";
import { prisma } from "@/db/client";
import { getPublicClient } from "@/lib/rpc";
import { CHAINS, type Chain } from "@/lib/types";
import { getTokenMetadata } from "@/lib/tokens";
import { getUsdMin } from "@/lib/env";

type PriceSource = "stable" | "chainlink";

interface PriceResult {
  price: number;
  source: PriceSource;
}

interface PriceRange {
  chain: Chain;
  fromBlock?: bigint;
  toBlock?: bigint;
  usdMinOverride?: number;
}

interface PriceSummary {
  priced: number;
  missing: number;
  filtered: number;
}

const STABLE_TOKENS: Record<Chain, Set<string>> = {
  eth: new Set(
    [
      "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48", // USDC
      "0xdac17f958d2ee523a2206206994597c13d831ec7", // USDT
      "0x6b175474e89094c44da98b954eedeac495271d0f", // DAI
    ].map((addr) => addr.toLowerCase()),
  ),
  arb: new Set(
    [
      "0xaf88d065e77c8cC2239327C5EDb3A432268e5831", // USDC
      "0xfd086bc7cd5c481dcc9c85ebe478a1c0b69fcbb9", // USDT
      "0xda10009cbd5d07dd0cecc66161fc93d7c9000da1", // DAI
    ].map((addr) => addr.toLowerCase()),
  ),
  base: new Set(
    [
      "0x833589fcd6edb6e08f4c7c32d4f71b54bdeebc1b", // USDC
      "0xd37ee7e4f452c6638c96536e68090de8cbcdb583", // DAI
    ].map((addr) => addr.toLowerCase()),
  ),
};

const CHAINLINK_USD_FEEDS: Record<Chain, Record<string, Address>> = {
  eth: {
    "0xc02aa39b223fe8d0a0e5c4f27ead9083c756cc2": getAddress(
      "0x5f4ec3df9cbd43714fe2740f5e3616155c5b8419",
    ), // ETH / USD
    "0x2260fac5e5542a773aa44fbcfedf7c193bc2c599": getAddress(
      "0xf4030086522a5beea4988f8ca5b36dbc97bee88c",
    ), // WBTC / USD
  },
  arb: {
    "0x82af49447d8a07e3bd95bd0d56f35241523fbab1": getAddress(
      "0x639fe6ab55c921f74e7fac1ee960c0b6293ba612",
    ), // WETH / USD
    "0x2f2a2543b76a4166549f7aaab494d819d9f93d2c": getAddress(
      "0xc907e116054ad103354f2d350fd2514433d57f6f",
    ), // WBTC / USD
  },
  base: {
    "0x4200000000000000000000000000000000000006": getAddress(
      "0x5fb16131dF4c65c1a7857aB8B6b969a8dEEfBc63",
    ), // WETH / USD
  },
};

const priceCache = new Map<string, PriceResult>();

function priceCacheKey(chain: Chain, address: string) {
  return `${chain}:${address.toLowerCase()}`;
}

function isStable(chain: Chain, address: string) {
  const set = STABLE_TOKENS[chain];
  return set?.has(address.toLowerCase()) ?? false;
}

async function readChainlinkPrice(
  chain: Chain,
  feedAddress: Address,
  blockNumber: bigint,
): Promise<number | null> {
  const client = getPublicClient(chain);
  const [roundData, decimals] = await Promise.all([
    client
      .readContract({
        address: feedAddress,
        abi: chainlinkAbi,
        functionName: "latestRoundData",
        blockNumber,
      })
      .catch(() => undefined),
    client
      .readContract({
        address: feedAddress,
        abi: chainlinkAbi,
        functionName: "decimals",
        blockNumber,
      })
      .catch(() => undefined),
  ]);

  if (!roundData || typeof decimals !== "number") {
    return null;
  }

  const [, answer] = roundData as [bigint, bigint, bigint, bigint, bigint];
  if (answer <= 0n) return null;
  const divisor = 10 ** decimals;
  return Number(answer) / divisor;
}

async function getUsdPrice(
  chain: Chain,
  tokenAddress: string,
  blockNumber: bigint,
): Promise<PriceResult | null> {
  const normalized = getAddress(tokenAddress);
  const cacheKey = priceCacheKey(chain, normalized);
  const cached = priceCache.get(cacheKey);
  if (cached) return cached;

  if (isStable(chain, normalized)) {
    const result: PriceResult = { price: 1, source: "stable" };
    priceCache.set(cacheKey, result);
    return result;
  }

  const feed = CHAINLINK_USD_FEEDS[chain]?.[normalized.toLowerCase()];
  if (feed) {
    const price = await readChainlinkPrice(chain, feed, blockNumber);
    if (price) {
      const result: PriceResult = { price, source: "chainlink" };
      priceCache.set(cacheKey, result);
      return result;
    }
  }

  return null;
}

function toDecimal(amountRaw: string, decimals: number): number {
  if (!amountRaw) return 0;
  const value = BigInt(amountRaw);
  if (value === 0n) return 0;
  const divisor = 10 ** decimals;
  return Number(value) / divisor;
}

export async function priceSwaps({
  chain,
  fromBlock,
  toBlock,
  usdMinOverride,
}: PriceRange): Promise<PriceSummary> {
  const usdMin = usdMinOverride ?? getUsdMin();
  const chainId = CHAINS[chain].id;

  const swaps = await prisma.swap.findMany({
    where: {
      chainId,
      ...(fromBlock
        ? {
            blockNumber: {
              gte: fromBlock,
            },
          }
        : {}),
      ...(toBlock
        ? {
            blockNumber: {
              lte: toBlock,
            },
          }
        : {}),
    },
    orderBy: [
      { blockNumber: "asc" },
      { logIndex: "asc" },
    ],
  });

  if (swaps.length === 0) {
    return { priced: 0, missing: 0, filtered: 0 };
  }

  const updates: Array<{
    id: bigint;
    usdIn: string | null;
    usdOut: string | null;
    usdNotional: string;
  }> = [];
  const toDelete: bigint[] = [];

  let missing = 0;

  for (const swap of swaps) {
    const tokenInMeta = await getTokenMetadata(chain, swap.tokenIn);
    const tokenOutMeta = await getTokenMetadata(chain, swap.tokenOut);

    const amountIn = toDecimal(swap.amountInRaw, tokenInMeta.decimals);
    const amountOut = toDecimal(swap.amountOutRaw, tokenOutMeta.decimals);

    const priceIn = await getUsdPrice(chain, swap.tokenIn, swap.blockNumber);
    const priceOut = await getUsdPrice(chain, swap.tokenOut, swap.blockNumber);

    const usdIn = priceIn && amountIn ? amountIn * priceIn.price : null;
    const usdOut = priceOut && amountOut ? amountOut * priceOut.price : null;
    const notional = Math.max(usdIn ?? 0, usdOut ?? 0);

    if (notional === 0) {
      missing += 1;
      continue;
    }

    if (notional < usdMin) {
      toDelete.push(swap.id);
      continue;
    }

    updates.push({
      id: swap.id,
      usdIn: usdIn !== null ? usdIn.toFixed(6) : null,
      usdOut: usdOut !== null ? usdOut.toFixed(6) : null,
      usdNotional: notional.toFixed(6),
    });
  }

  if (updates.length > 0) {
    const chunkSize = 100;
    for (let i = 0; i < updates.length; i += chunkSize) {
      const slice = updates.slice(i, i + chunkSize);
      await prisma.$transaction(
        slice.map(({ id, usdIn, usdOut, usdNotional }) =>
          prisma.swap.update({
            where: { id },
            data: {
              usdIn,
              usdOut,
              usdNotional,
            },
          }),
        ),
      );
    }
  }

  let filtered = 0;

  if (toDelete.length > 0) {
    const deleted = await prisma.swap.deleteMany({
      where: {
        id: {
          in: toDelete,
        },
      },
    });
    filtered = deleted.count;
  }

  return {
    priced: updates.length,
    missing,
    filtered,
  };
}

