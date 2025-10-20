import { getAddress, type Address } from "viem";
import { erc20Abi } from "@/abi/erc20";
import { prisma } from "@/db/client";
import { getPublicClient } from "@/lib/rpc";
import { CHAINS, type Chain } from "@/lib/types";

export interface TokenMetadata {
  address: Address;
  symbol: string;
  decimals: number;
}

const tokenCache = new Map<string, TokenMetadata>();

function cacheKey(chain: Chain, address: Address) {
  return `${chain}:${address.toLowerCase()}`;
}

export async function getTokenMetadata(
  chain: Chain,
  address: string,
): Promise<TokenMetadata> {
  const normalized = getAddress(address);
  const key = cacheKey(chain, normalized);
  const cached = tokenCache.get(key);
  if (cached) return cached;

  const chainId = CHAINS[chain].id;
  let token = await prisma.token.findFirst({
    where: {
      chainId,
      address: normalized,
    },
  });

  if (!token) {
    const client = getPublicClient(chain);
    const [symbol, decimals] = await Promise.all([
      client
        .readContract({
          address: normalized,
          abi: erc20Abi,
          functionName: "symbol",
        })
        .catch(() => undefined),
      client
        .readContract({
          address: normalized,
          abi: erc20Abi,
          functionName: "decimals",
        })
        .catch(() => undefined),
    ]);

    const resolvedSymbol =
      typeof symbol === "string" && symbol.length > 0 ? symbol : "UNKNOWN";
    const resolvedDecimals =
      typeof decimals === "number" ? decimals : Number(decimals ?? 18);

    token = await prisma.token.upsert({
      where: {
        chainId_address: {
          chainId,
          address: normalized,
        },
      },
      create: {
        chainId,
        address: normalized,
        symbol: resolvedSymbol,
        decimals: resolvedDecimals,
      },
      update: {
        symbol: resolvedSymbol,
        decimals: resolvedDecimals,
      },
    });
  }

  const metadata: TokenMetadata = {
    address: getAddress(token.address),
    symbol: token.symbol,
    decimals: token.decimals,
  };

  tokenCache.set(key, metadata);
  return metadata;
}

