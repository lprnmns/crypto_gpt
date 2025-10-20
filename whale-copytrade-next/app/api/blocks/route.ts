import { NextResponse } from "next/server";
import { z } from "zod";
import { getWindowBlocks } from "@/lib/blocks";

const requestSchema = z.object({
  chain: z.enum(["eth", "arb", "base"]),
  fromIso: z.string().datetime(),
  toIso: z.string().datetime(),
});

export async function POST(request: Request) {
  try {
    const payload = await request.json();
    const body = requestSchema.parse(payload);
    const result = await getWindowBlocks(body);
    return NextResponse.json(result);
  } catch (error) {
    if (error instanceof z.ZodError) {
      return NextResponse.json(
        { error: "Geçersiz istek", issues: error.flatten() },
        { status: 400 },
      );
    }
    const message =
      error instanceof Error ? error.message : "Bilinmeyen hata oluştu";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
