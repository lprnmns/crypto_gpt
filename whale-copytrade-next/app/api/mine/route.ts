import { NextResponse } from "next/server";
import { z } from "zod";
import { mineCandidates } from "@/lib/mine";

const requestSchema = z.object({
  chain: z.enum(["eth", "arb", "base", "all"]).optional(),
  usdMin: z.coerce.number().optional(),
});

export async function POST(request: Request) {
  try {
    const payload = await request.json();
    const body = requestSchema.parse(payload);

    const candidates = await mineCandidates({
      chain: body.chain === undefined ? "all" : body.chain,
      usdMinOverride: body.usdMin,
    });

    return NextResponse.json({
      candidates,
      count: candidates.length,
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

