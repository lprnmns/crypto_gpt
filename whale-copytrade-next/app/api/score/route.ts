import { NextResponse } from "next/server";
import { z } from "zod";
import { mineCandidates } from "@/lib/mine";
import { evaluateCandidates } from "@/lib/pnl";
import { rankCandidates, type ScoreWeights } from "@/lib/score";

const requestSchema = z.object({
  chain: z.enum(["eth", "arb", "base", "all"]).optional(),
  usdMin: z.coerce.number().optional(),
  mode: z.enum(["fifo", "lifo"]).optional(),
  limit: z.coerce.number().optional(),
  weights: z
    .object({
      t1Pnl: z.number().optional(),
      t7Pnl: z.number().optional(),
      winRate: z.number().optional(),
      tradeRatio: z.number().optional(),
      impact: z.number().optional(),
      repeatability: z.number().optional(),
      liquidity: z.number().optional(),
    })
    .optional(),
});

export async function POST(request: Request) {
  try {
    const payload = await request.json();
    const body = requestSchema.parse(payload);

    const candidates = await mineCandidates({
      chain: body.chain ?? "all",
      usdMinOverride: body.usdMin,
    });

    const pnls = await evaluateCandidates(
      candidates,
      body.mode ?? "fifo",
    );

    const scores = rankCandidates(
      candidates,
      pnls,
      body.weights as ScoreWeights | undefined,
    );

    return NextResponse.json({
      count: scores.length,
      items: body.limit ? scores.slice(0, body.limit) : scores,
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

