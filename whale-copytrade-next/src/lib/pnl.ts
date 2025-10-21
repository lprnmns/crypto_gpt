import { Decimal } from "@prisma/client/runtime/library";
import { prisma } from "@/db/client";
import type { Candidate } from "@/lib/mine";

export type PnlMode = "fifo" | "lifo";

interface SwapRow {
  chainId: number;
  tokenIn: string;
  tokenOut: string;
  usdIn: Decimal | null;
  usdOut: Decimal | null;
  usdNotional: Decimal | null;
  timestamp: Date;
}

interface Position {
  token: string;
  remaining: number;
  costBasis: number;
  timestamp: Date;
}

interface PnlResult {
  realized: number;
  gross: number;
  trades: number;
  realizedTokens: number;
}

const BATCH_SIZE = 100;

function decimal(value: Decimal | null): number {
  if (!value) return 0;
  if (typeof value === "number") return value;
  if (typeof (value as unknown as { toNumber?: () => number }).toNumber === "function") {
    return (value as unknown as { toNumber: () => number }).toNumber();
  }
  return Number(value);
}

function settlePosition(
  position: Position,
  usdOut: number,
  timestamp: Date,
): number {
  const gain = usdOut - position.costBasis;
  position.remaining = 0;
  position.costBasis = 0;
  position.timestamp = timestamp;
  return gain;
}

function reducePositions(
  positions: Position[],
  amount: number,
  usdOut: number,
  mode: PnlMode,
  timestamp: Date,
): number {
  let remaining = amount;
  let realized = 0;

  const iterator =
    mode === "fifo"
      ? positions.values()
      : (function* lifo() {
          for (let i = positions.length - 1; i >= 0; i -= 1) {
            yield positions[i];
          }
        })();

  for (const position of iterator) {
    if (remaining <= 0) break;
    if (position.remaining <= 0) continue;

    const portion = Math.min(position.remaining, remaining);
    const costRatio = portion / position.remaining;
    const cost = position.costBasis * costRatio;
    const revenue = usdOut * costRatio;

    realized += revenue - cost;
    position.remaining -= portion;
    position.costBasis -= cost;
    position.timestamp = timestamp;
    remaining -= portion;
  }

  return realized;
}

async function fetchSwaps(wallet: string): Promise<SwapRow[]> {
  const result: SwapRow[] = [];
  let cursor: Date | null = null;

  for (;;) {
    const rows = await prisma.swap.findMany({
      where: { trader: wallet },
      orderBy: [
        { timestamp: "asc" },
        { blockNumber: "asc" },
        { logIndex: "asc" },
      ],
      take: BATCH_SIZE,
      ...(cursor
        ? {
            skip: 1,
            cursor: {
              trader_timestamp_blockNumber_logIndex: {
                trader: wallet,
                timestamp: cursor,
                blockNumber: 0n,
                logIndex: 0,
              },
            },
          }
        : {}),
      select: {
        chainId: true,
        tokenIn: true,
        tokenOut: true,
        usdIn: true,
        usdOut: true,
        usdNotional: true,
        timestamp: true,
      },
    });

    if (rows.length === 0) break;

    result.push(...rows);
    const last = rows[rows.length - 1];
    cursor = last.timestamp;
    if (rows.length < BATCH_SIZE) break;
  }

  return result;
}

export async function evaluateCandidate(
  candidate: Candidate,
  mode: PnlMode = "fifo",
): Promise<PnlResult> {
  const wallet = candidate.wallet.toLowerCase();
  const swaps = await fetchSwaps(wallet);

  const positions = new Map<string, Position[]>();
  let realized = 0;
  let gross = 0;
  let trades = 0;
  let realizedTokens = 0;

  for (const swap of swaps) {
    const keyIn = `${swap.chainId}:${swap.tokenIn.toLowerCase()}`;
    const keyOut = `${swap.chainId}:${swap.tokenOut.toLowerCase()}`;

    const usdIn = decimal(swap.usdIn);
    const usdOut = decimal(swap.usdOut);
    const notional = decimal(swap.usdNotional);

    if (usdIn > 0) {
      const list = positions.get(keyIn) ?? [];
      list.push({
        token: swap.tokenIn,
        remaining: usdIn,
        costBasis: usdIn,
        timestamp: swap.timestamp,
      });
      positions.set(keyIn, list);
      gross += usdIn;
      trades += 1;
    }

    if (usdOut > 0) {
      const list = positions.get(keyOut) ?? [];
      if (list.length === 0) {
        gross += usdOut;
        trades += 1;
        continue;
      }
      realized += reducePositions(list, usdOut, usdOut, mode, swap.timestamp);
      realizedTokens += 1;
    }

    if (notional > gross) {
      gross = notional;
    }
  }

  return {
    realized,
    gross,
    trades,
    realizedTokens,
  };
}

export async function evaluateCandidates(
  candidates: Candidate[],
  mode: PnlMode = "fifo",
): Promise<Record<string, PnlResult>> {
  const result: Record<string, PnlResult> = {};
  for (const candidate of candidates) {
    result[candidate.wallet] = await evaluateCandidate(candidate, mode);
  }
  return result;
}

