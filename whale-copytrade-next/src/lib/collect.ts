import {
  decodeEventLog,
  getAddress,
  type Address,
  type Hex,
  type Log,
} from "viem";
import { getPublicClient, withRetry } from "@/lib/rpc";
import { CHAINS, type Chain } from "@/lib/types";
import { univ2PairAbi, univ2SwapEvent } from "@/abi/univ2";
import { univ3PoolAbi, univ3SwapEvent } from "@/abi/univ3";
import type { SwapLog } from "@/lib/dex";

type DexKind = "univ2" | "univ3";

const MAX_BLOCK_SPAN: Record<Chain, bigint> = {
  eth: 10n,
  arb: 1000n,
  base: 1000n,
};

interface CollectParams {
  chain: Chain;
  fromBlock: bigint;
  toBlock: bigint;
}

interface CollectResult {
  items: SwapLog[];
  totalLogs: number;
}

type Tokens = {
  token0: string;
  token1: string;
};

type TxInfo = {
  from: string;
  to?: string;
};

export async function collectSwaps({
  chain,
  fromBlock,
  toBlock,
}: CollectParams): Promise<CollectResult> {
  const client = getPublicClient(chain);
  const chainId = CHAINS[chain].id;

  const blockTimestampCache = new Map<bigint, Date>();
  const poolTokenCache = new Map<string, Tokens>();
  const transactionCache = new Map<Hex, TxInfo>();

  const isBlockRangeError = (error: unknown): boolean => {
    if (
      typeof error !== "object" ||
      error === null ||
      !("status" in error) ||
      !("details" in error)
    ) {
      return false;
    }
    const status = (error as { status?: number }).status;
    const details = (error as { details?: unknown }).details;
    if (status !== 400 || typeof details !== "string") {
      return false;
    }
    return details.toLowerCase().includes("block range");
  };

  const getBlockTimestamp = async (blockNumber: bigint): Promise<Date> => {
    const cached = blockTimestampCache.get(blockNumber);
    if (cached) return cached;

    const block = await withRetry(() =>
      client.getBlock({
        blockNumber,
      }),
    );

    const timestamp = new Date(Number(block.timestamp) * 1000);
    blockTimestampCache.set(blockNumber, timestamp);
    return timestamp;
  };

  const getPoolTokens = async (
    pool: string,
    dex: "univ2" | "univ3",
  ): Promise<Tokens> => {
    const key = `${dex}:${pool}`;
    const cached = poolTokenCache.get(key);
    if (cached) return cached;

    const abi = dex === "univ2" ? univ2PairAbi : univ3PoolAbi;
    const address = pool as Address;
    const [token0, token1] = await Promise.all([
      withRetry(() =>
        client.readContract({
          address,
          abi,
          functionName: "token0",
        }),
      ),
      withRetry(() =>
        client.readContract({
          address,
          abi,
          functionName: "token1",
        }),
      ),
    ]);

    const value = {
      token0: getAddress(token0 as Address),
      token1: getAddress(token1 as Address),
    };
    poolTokenCache.set(key, value);
    return value;
  };

  const getTransactionInfo = async (hash: Hex): Promise<TxInfo> => {
    const cached = transactionCache.get(hash);
    if (cached) return cached;

    const tx = await withRetry(() =>
      client.getTransaction({
        hash,
      }),
    );

    const info: TxInfo = {
      from: getAddress(tx.from),
      to: tx.to ? getAddress(tx.to) : undefined,
    };

    transactionCache.set(hash, info);
    return info;
  };

  const fetchLogs = async (
    event: typeof univ2SwapEvent | typeof univ3SwapEvent,
  ): Promise<Log[]> => {
    const logs: Log[] = [];
    const defaultSpan = MAX_BLOCK_SPAN[chain] ?? 100n;
    let span = defaultSpan;
    let cursor = fromBlock;

    while (cursor <= toBlock) {
      const chunkEnd = cursor + span - 1n > toBlock ? toBlock : cursor + span - 1n;
      try {
        const chunk = await withRetry(() =>
          client.getLogs({
            fromBlock: cursor,
            toBlock: chunkEnd,
            event,
          }),
        );
        logs.push(...chunk);
        cursor = chunkEnd + 1n;
        if (span < defaultSpan) {
          span = span * 2n > defaultSpan ? defaultSpan : span * 2n;
        }
      } catch (error) {
        if (!isBlockRangeError(error)) {
          throw error;
        }
        span = span / 2n;
        if (span < 1n) {
          span = 1n;
        }
        continue;
      }
    }

    return logs;
  };

  const rawLogsV2 = await fetchLogs(univ2SwapEvent);
  const rawLogsV3 = await fetchLogs(univ3SwapEvent);

  const combinedLogs: Array<{ dex: DexKind; log: Log }> = [
    ...rawLogsV2.map((log) => ({ dex: "univ2" as const, log })),
    ...rawLogsV3.map((log) => ({ dex: "univ3" as const, log })),
  ];

  const items: SwapLog[] = [];

  for (const { dex, log } of combinedLogs) {
    if (!log.blockNumber || !log.transactionHash) continue;

    const pool = getAddress(log.address);
    const timestamp = await getBlockTimestamp(log.blockNumber);
    const tx = await getTransactionInfo(log.transactionHash);

    if (dex === "univ2") {
      const decoded = decodeEventLog({
        abi: [univ2SwapEvent],
        data: log.data,
        topics: log.topics,
      });
      const { amount0In, amount1In, amount0Out, amount1Out } = decoded
        .args as {
        amount0In: bigint;
        amount1In: bigint;
        amount0Out: bigint;
        amount1Out: bigint;
      };
      const tokens = await getPoolTokens(pool, "univ2");

      const tokenIn =
        amount0In > 0n ? tokens.token0 : tokens.token1;
      const tokenOut =
        amount0Out > 0n ? tokens.token0 : tokens.token1;

      const amountInRaw =
        amount0In > 0n ? amount0In : amount1In;
      const amountOutRaw =
        amount0Out > 0n ? amount0Out : amount1Out;

      items.push({
        chainId,
        blockNumber: log.blockNumber,
        blockTimestamp: timestamp,
        txHash: log.transactionHash,
        logIndex: Number(log.logIndex),
        pool,
        trader: tx.from,
        router: tx.to,
        tokenIn,
        amountInRaw,
        tokenOut,
        amountOutRaw,
        dex: "univ2",
        viaAggregator: tx.to ? tx.to !== pool : false,
      });
      continue;
    }

    if (dex === "univ3") {
      const decoded = decodeEventLog({
        abi: [univ3SwapEvent],
        data: log.data,
        topics: log.topics,
      });
      const { amount0, amount1, recipient } = decoded.args as {
        amount0: bigint;
        amount1: bigint;
        recipient: Address;
      };
      const tokens = await getPoolTokens(pool, "univ3");

      let tokenIn = "";
      let tokenOut = "";
      let amountInRaw = 0n;
      let amountOutRaw = 0n;

      if (amount0 > 0n) {
        tokenIn = tokens.token0;
        amountInRaw = amount0;
      } else if (amount0 < 0n) {
        tokenOut = tokens.token0;
        amountOutRaw = -amount0;
      }

      if (amount1 > 0n) {
        tokenIn = tokens.token1;
        amountInRaw = amount1;
      } else if (amount1 < 0n) {
        tokenOut = tokens.token1;
        amountOutRaw = -amount1;
      }

      items.push({
        chainId,
        blockNumber: log.blockNumber,
        blockTimestamp: timestamp,
        txHash: log.transactionHash,
        logIndex: Number(log.logIndex),
        pool,
        trader: tx.from,
        router: tx.to,
        tokenIn,
        amountInRaw,
        tokenOut,
        amountOutRaw,
        dex: "univ3",
        viaAggregator:
          (tx.to ? tx.to !== pool : false) ||
          getAddress(recipient) !== tx.from,
      });
    }
  }

  items.sort((a, b) => {
    if (a.blockNumber === b.blockNumber) {
      return a.logIndex - b.logIndex;
    }
    return Number(a.blockNumber - b.blockNumber);
  });

  return {
    items,
    totalLogs: combinedLogs.length,
  };
}
