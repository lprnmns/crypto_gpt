import { Decimal } from "@prisma/client/runtime/library";
import { prisma } from "@/db/client";
import { CHAINS, type Chain } from "@/lib/types";
import { getMiningWindows, getUsdMin } from "@/lib/env";

interface WindowAggregate {
  usdIn: number;
  usdOut: number;
  volume: number;
  swaps: number;
}

export interface Candidate {
  wallet: string;
  w1Net: number;
  w1Volume: number;
  w1Swaps: number;
  w2Net: number;
  w2Volume: number;
  w2Swaps: number;
  chains: number[];
}

export interface MineOptions {
  chain?: Chain | "all";
  usdMinOverride?: number;
}

function decimalToNumber(value: Decimal | null): number {
  if (!value) return 0;
  if (typeof value === "number") return value;
  if (typeof (value as unknown as { toNumber?: () => number }).toNumber === "function") {
    return (value as unknown as { toNumber: () => number }).toNumber();
  }
  return Number(value);
}

function aggregateSwaps(
  rows: Array<{
    trader: string;
    chainId: number;
    usdIn: Decimal | null;
    usdOut: Decimal | null;
    usdNotional: Decimal | null;
  }>,
): Map<
  string,
  {
    chains: Set<number>;
    data: WindowAggregate;
  }
> {
  const map = new Map<
    string,
    {
      chains: Set<number>;
      data: WindowAggregate;
    }
  >();

  for (const row of rows) {
    const trader = row.trader.toLowerCase();
    const entry =
      map.get(trader) ??
      {
        chains: new Set<number>(),
        data: { usdIn: 0, usdOut: 0, volume: 0, swaps: 0 },
      };

    entry.chains.add(row.chainId);
    entry.data.usdIn += decimalToNumber(row.usdIn);
    entry.data.usdOut += decimalToNumber(row.usdOut);
    entry.data.volume += decimalToNumber(row.usdNotional);
    entry.data.swaps += 1;

    map.set(trader, entry);
  }

  return map;
}

export async function mineCandidates({
  chain,
  usdMinOverride,
}: MineOptions = {}): Promise<Candidate[]> {
  const usdMin = usdMinOverride ?? getUsdMin();
  const { W1, W2 } = getMiningWindows();
  const chainsFilter =
    chain && chain !== "all" ? [CHAINS[chain].id] : Object.values(CHAINS).map((c) => c.id);

  const w1Rows = await prisma.swap.findMany({
    where: {
      chainId: { in: chainsFilter },
      timestamp: {
        gte: W1.start,
        lt: W1.end,
      },
    },
    select: {
      trader: true,
      chainId: true,
      usdIn: true,
      usdOut: true,
      usdNotional: true,
    },
  });

  const w2Rows = await prisma.swap.findMany({
    where: {
      chainId: { in: chainsFilter },
      timestamp: {
        gte: W2.start,
        lt: W2.end,
      },
    },
    select: {
      trader: true,
      chainId: true,
      usdIn: true,
      usdOut: true,
      usdNotional: true,
    },
  });

  const w1Map = aggregateSwaps(w1Rows);
  const w2Map = aggregateSwaps(w2Rows);
  const candidates: Candidate[] = [];

  for (const [wallet, w1Entry] of w1Map.entries()) {
    const w2Entry = w2Map.get(wallet);
    if (!w2Entry) continue;

    const w1Net = w1Entry.data.usdOut - w1Entry.data.usdIn;
    const w2Net = w2Entry.data.usdIn - w2Entry.data.usdOut;

    if (w1Net < usdMin || w2Net < usdMin) continue;

    const chains = new Set<number>([
      ...w1Entry.chains.values(),
      ...w2Entry.chains.values(),
    ]);

    candidates.push({
      wallet,
      w1Net,
      w1Volume: w1Entry.data.volume,
      w1Swaps: w1Entry.data.swaps,
      w2Net,
      w2Volume: w2Entry.data.volume,
      w2Swaps: w2Entry.data.swaps,
      chains: Array.from(chains.values()),
    });
  }

  candidates.sort((a, b) => b.w2Net - a.w2Net);
  return candidates;
}
