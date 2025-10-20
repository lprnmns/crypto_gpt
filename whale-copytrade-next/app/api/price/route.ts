import { NextResponse } from "next/server";
import { z } from "zod";
import { priceSwaps } from "@/lib/price";

const requestSchema = z
  .object({
    chain: z.enum(["eth", "arb", "base"]),
    fromBlock: z.coerce.bigint().optional(),
    toBlock: z.coerce.bigint().optional(),
    usdMin: z.coerce.number().optional(),
  })
  .refine(
    (data) =>
      !(data.fromBlock && data.toBlock && data.fromBlock > data.toBlock),
    {
      message: "fromBlock must be less than or equal to toBlock.",
      path: ["fromBlock"],
    },
  );

export async function POST(request: Request) {
  try {
    const payload = await request.json();
    const body = requestSchema.parse(payload);

    const started = Date.now();

    const summary = await priceSwaps({
      chain: body.chain,
      fromBlock: body.fromBlock,
      toBlock: body.toBlock,
      usdMinOverride: body.usdMin,
    });

    return NextResponse.json({
      ...summary,
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

