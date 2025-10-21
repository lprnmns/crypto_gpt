import type { Candidate } from "@/lib/mine";
import type { PnlResult } from "@/lib/pnl";

export interface ScoreWeights {
  t1Pnl?: number;
  t7Pnl?: number;
  winRate?: number;
  tradeRatio?: number;
  impact?: number;
  repeatability?: number;
  liquidity?: number;
}

const DEFAULT_WEIGHTS: Required<ScoreWeights> = {
  t1Pnl: 0.3,
  t7Pnl: 0.2,
  winRate: 0.1,
  tradeRatio: 0.15,
  impact: 0.1,
  repeatability: 0.1,
  liquidity: 0.05,
};

export interface ScoreMetrics {
  normalized: Required<ScoreWeights>;
  raw: {
    realized: number;
    trades: number;
    realizedTokens: number;
    w1Net: number;
    w2Net: number;
    w1Volume: number;
    w2Volume: number;
    chainCount: number;
  };
}

export interface CandidateScore {
  wallet: string;
  score: number;
  metrics: ScoreMetrics;
}

function clamp(value: number, min = 0, max = 1) {
  return Math.min(max, Math.max(min, value));
}

function sumWeights(weights: ScoreWeights): number {
  return (
    (weights.t1Pnl ?? 0) +
    (weights.t7Pnl ?? 0) +
    (weights.winRate ?? 0) +
    (weights.tradeRatio ?? 0) +
    (weights.impact ?? 0) +
    (weights.repeatability ?? 0) +
    (weights.liquidity ?? 0)
  );
}

function mergeWeights(weights?: ScoreWeights): Required<ScoreWeights> {
  if (!weights) return { ...DEFAULT_WEIGHTS };
  const merged = { ...DEFAULT_WEIGHTS, ...weights };
  const total = sumWeights(merged);

  if (total === 0) {
    return { ...DEFAULT_WEIGHTS };
  }

  return {
    t1Pnl: merged.t1Pnl / total,
    t7Pnl: merged.t7Pnl / total,
    winRate: merged.winRate / total,
    tradeRatio: merged.tradeRatio / total,
    impact: merged.impact / total,
    repeatability: merged.repeatability / total,
    liquidity: merged.liquidity / total,
  };
}

function normalizeMetrics(candidate: Candidate, pnl: PnlResult) {
  const netGain = candidate.w1Net + candidate.w2Net;
  const totalVolume = candidate.w1Volume + candidate.w2Volume;
  const trades = pnl.trades || 1;

  const t1Norm = clamp(pnl.realized / 50_000);
  const t7Norm = clamp(netGain / 100_000);
  const winRateNorm = clamp(pnl.realizedTokens / trades);
  const tradeRatioNorm = clamp(candidate.w2Swaps / (candidate.w1Swaps + candidate.w2Swaps || 1));
  const impactNorm = clamp(1 - totalVolume / 2_000_000);
  const repeatNorm = clamp(candidate.chains.length / 4);
  const liquidityNorm = clamp(totalVolume / 1_000_000);

  return {
    normalized: {
      t1Pnl: t1Norm,
      t7Pnl: t7Norm,
      winRate: winRateNorm,
      tradeRatio: tradeRatioNorm,
      impact: impactNorm,
      repeatability: repeatNorm,
      liquidity: liquidityNorm,
    },
    raw: {
      realized: pnl.realized,
      trades: pnl.trades,
      realizedTokens: pnl.realizedTokens,
      w1Net: candidate.w1Net,
      w2Net: candidate.w2Net,
      w1Volume: candidate.w1Volume,
      w2Volume: candidate.w2Volume,
      chainCount: candidate.chains.length,
    },
  };
}

export function scoreCandidate(
  candidate: Candidate,
  pnl: PnlResult,
  weights?: ScoreWeights,
): CandidateScore {
  const weightMap = mergeWeights(weights);
  const metrics = normalizeMetrics(candidate, pnl);

  const score =
    metrics.normalized.t1Pnl * weightMap.t1Pnl +
    metrics.normalized.t7Pnl * weightMap.t7Pnl +
    metrics.normalized.winRate * weightMap.winRate +
    metrics.normalized.tradeRatio * weightMap.tradeRatio +
    metrics.normalized.impact * weightMap.impact +
    metrics.normalized.repeatability * weightMap.repeatability +
    metrics.normalized.liquidity * weightMap.liquidity;

  return {
    wallet: candidate.wallet,
    score,
    metrics: {
      normalized: metrics.normalized,
      raw: metrics.raw,
    },
  };
}

export function rankCandidates(
  candidates: Candidate[],
  pnls: Record<string, PnlResult>,
  weights?: ScoreWeights,
): CandidateScore[] {
  const scores = candidates
    .map((candidate) => {
      const pnl = pnls[candidate.wallet] ?? {
        realized: 0,
        gross: 0,
        trades: 0,
        realizedTokens: 0,
      };
      return scoreCandidate(candidate, pnl, weights);
    })
    .sort((a, b) => b.score - a.score);

  return scores;
}

