import { describe, expect, it } from "vitest";

const candidate = {
  wallet: "0xwallet1",
  w1Net: 20_000,
  w1Volume: 30_000,
  w1Swaps: 4,
  w2Net: 40_000,
  w2Volume: 50_000,
  w2Swaps: 5,
  chains: [1, 42161],
};

const pnl = {
  realized: 25_000,
  gross: 70_000,
  trades: 6,
  realizedTokens: 4,
};

describe("scoreCandidate", () => {
  it("computes weighted score with defaults", async () => {
    const { scoreCandidate } = await import("@/lib/score");

    const result = scoreCandidate(candidate, pnl);

    expect(result.wallet).toBe(candidate.wallet);
    expect(result.score).toBeGreaterThan(0);
    expect(result.score).toBeLessThanOrEqual(1);
  });

  it("normalizes custom weights", async () => {
    const { scoreCandidate } = await import("@/lib/score");

    const result = scoreCandidate(candidate, pnl, {
      t1Pnl: 1,
      t7Pnl: 0,
      winRate: 0,
      tradeRatio: 0,
      impact: 0,
      repeatability: 0,
      liquidity: 0,
    });

    expect(result.metrics.normalized.t1Pnl).toBeGreaterThan(0);
    expect(result.metrics.normalized.t7Pnl).toBeGreaterThan(0);
    expect(result.score).toBe(result.metrics.normalized.t1Pnl);
  });

  it("ranks candidates descending", async () => {
    const { rankCandidates } = await import("@/lib/score");

    const scores = rankCandidates(
      [
        candidate,
        {
          ...candidate,
          wallet: "0xwallet2",
          w2Net: 20_000,
        },
      ],
      {
        [candidate.wallet]: pnl,
        "0xwallet2": { ...pnl, realized: 5_000 },
      },
    );

    expect(scores).toHaveLength(2);
    expect(scores[0].wallet).toBe(candidate.wallet);
  });
});

