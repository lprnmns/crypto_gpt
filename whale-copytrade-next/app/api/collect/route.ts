import { NextResponse } from "next/server";
import { z } from "zod";
import { collectSwaps } from "@/lib/collect";
import { priceSwaps } from "@/lib/price";
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

    const data = items.map((item) => ({
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
    }));

    let inserted = 0;
    if (data.length > 0) {
      const result = await prisma.swap.createMany({
        data,
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
      collected: items.length,
      inserted,
      priced: priceSummary.priced,
      missingPrices: priceSummary.missing,
      filtered: priceSummary.filtered,
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

