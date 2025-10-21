import { NextResponse } from "next/server";
import { z } from "zod";
import { collectSwaps } from "@/lib/collect";
import { priceSwaps } from "@/lib/price";
import { getLabelMap, hasExclusionLabel } from "@/lib/classify";
import { prisma } from "@/db/client";

const requestSchema = z.object({
  chain: z.enum(["eth", "arb", "base"]),
  fromBlock: z.coerce.bigint(),
  toBlock: z.coerce.bigint(),
  usdMin: z.coerce.number().optional(),
});

export async function POST(request: Request) {
  try {
    const payload = await request.json();
    const body = requestSchema.parse(payload);

    if (body.fromBlock > body.toBlock) {
      return NextResponse.json(
        { error: "fromBlock must be less than or equal to toBlock." },
        { status: 400 },
      );
    }

    const started = Date.now();
    const { items, totalLogs } = await collectSwaps({
      chain: body.chain,
      fromBlock: body.fromBlock,
      toBlock: body.toBlock,
    });

    const labelAddresses = new Set<string>();
    for (const item of items) {
      labelAddresses.add(item.trader);
      if (item.router) labelAddresses.add(item.router);
      labelAddresses.add(item.pool);
    }

    const labelMap = await getLabelMap(Array.from(labelAddresses));

    const filteredItems: typeof items = [];
    const filteredData: Array<{
      chainId: number;
      txHash: string;
      logIndex: number;
      blockNumber: bigint;
      timestamp: Date;
      pool: string;
      router: string | null;
      trader: string;
      tokenIn: string;
      amountInRaw: string;
      tokenOut: string;
      amountOutRaw: string;
      usdIn: null;
      usdOut: null;
      usdNotional: null;
      dex: string;
      viaAggregator: boolean;
    }> = [];

    let labelFiltered = 0;

    for (let i = 0; i < items.length; i += 1) {
      const item = items[i];
      const mapped = {
        chainId: item.chainId,
        txHash: item.txHash,
        logIndex: item.logIndex,
        blockNumber: item.blockNumber,
        timestamp: item.blockTimestamp,
        pool: item.pool,
        router: item.router ?? null,
        trader: item.trader,
        tokenIn: item.tokenIn,
        amountInRaw: item.amountInRaw.toString(),
        tokenOut: item.tokenOut,
        amountOutRaw: item.amountOutRaw.toString(),
        usdIn: null,
        usdOut: null,
        usdNotional: null,
        dex: item.dex,
        viaAggregator: item.viaAggregator,
      };

      const excluded =
        hasExclusionLabel(item.trader, labelMap) ||
        hasExclusionLabel(item.router ?? undefined, labelMap) ||
        hasExclusionLabel(item.pool, labelMap);

      if (excluded) {
        labelFiltered += 1;
        continue;
      }

      filteredItems.push(item);
      filteredData.push(mapped);
    }

    let inserted = 0;
    if (filteredData.length > 0) {
      const result = await prisma.swap.createMany({
        data: filteredData,
        skipDuplicates: true,
      });
      inserted = result.count;
    }

    const priceSummary = await priceSwaps({
      chain: body.chain,
      fromBlock: body.fromBlock,
      toBlock: body.toBlock,
      usdMinOverride: body.usdMin,
    });

    return NextResponse.json({
      totalLogs,
      collected: filteredItems.length,
      inserted,
      labelFiltered,
      priced: priceSummary.priced,
      missingPrices: priceSummary.missing,
      usdFiltered: priceSummary.filtered,
      tookMs: Date.now() - started,
    });
  } catch (error) {
    if (error instanceof z.ZodError) {
      return NextResponse.json(
        { error: "Invalid request", issues: error.flatten() },
        { status: 400 },
      );
    }

    const message =
      error instanceof Error ? error.message : "Unexpected server error";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

