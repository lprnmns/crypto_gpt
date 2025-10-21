import { mineCandidates, type MineOptions } from "@/lib/mine";
import { evaluateCandidates, type PnlMode } from "@/lib/pnl";
import { rankCandidates, type CandidateScore, type ScoreWeights } from "@/lib/score";

export interface ScoreboardOptions extends MineOptions {
  mode?: PnlMode;
  limit?: number;
  weights?: ScoreWeights;
}

export async function getScoredCandidates({
  chain = "all",
  usdMinOverride,
  mode = "fifo",
  limit,
  weights,
}: ScoreboardOptions = {}): Promise<CandidateScore[]> {
  const candidates = await mineCandidates({
    chain,
    usdMinOverride,
  });

  if (candidates.length === 0) {
    return [];
  }

  const pnls = await evaluateCandidates(candidates, mode);
  const scores = rankCandidates(candidates, pnls, weights);

  return typeof limit === "number" && limit > 0 ? scores.slice(0, limit) : scores;
}

