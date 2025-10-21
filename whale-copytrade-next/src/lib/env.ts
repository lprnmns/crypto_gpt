import { Chain } from "@/lib/types";

type RpcKind = "http" | "ws";

const RPC_KEYS: Record<Chain, Record<RpcKind, string>> = {
  eth: { http: "ETH_HTTP_RPC", ws: "ETH_WS_RPC" },
  arb: { http: "ARB_HTTP_RPC", ws: "ARB_WS_RPC" },
  base: { http: "BASE_HTTP_RPC", ws: "BASE_WS_RPC" },
};

function requireEnv(key: string): string {
  const value = process.env[key];
  if (!value || value.trim().length === 0) {
    throw new Error(`.env içinde ${key} değeri tanımlı değil.`);
  }
  return value.trim();
}

export function getRpcUrl(chain: Chain, kind: RpcKind): string {
  const key = RPC_KEYS[chain][kind];
  return requireEnv(key);
}

export function getUsdMin(): number {
  const raw = process.env.USD_MIN ?? "30000";
  const parsed = Number(raw);
  if (Number.isNaN(parsed) || parsed <= 0) {
    throw new Error(`USD_MIN değeri sayıya çevrilemedi: ${raw}`);
  }
  return parsed;
}

function parseIsoDate(value: string | undefined, key: string): Date {
  if (!value) {
    throw new Error(`.env içinde ${key} değeri tanımlı değil.`);
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    throw new Error(`${key} değeri geçerli bir ISO tarihi değil: ${value}`);
  }
  return date;
}

export interface TimeWindow {
  start: Date;
  end: Date;
}

export interface MiningWindows {
  W1: TimeWindow;
  W2: TimeWindow;
}

export function getMiningWindows(): MiningWindows {
  const W1_START = parseIsoDate(process.env.W1_START, "W1_START");
  const W1_END = parseIsoDate(process.env.W1_END, "W1_END");
  const W2_START = parseIsoDate(process.env.W2_START, "W2_START");
  const W2_END = parseIsoDate(process.env.W2_END, "W2_END");

  if (W1_START >= W1_END) {
    throw new Error("W1_START değeri W1_END değerinden küçük olmalıdır.");
  }
  if (W2_START >= W2_END) {
    throw new Error("W2_START değeri W2_END değerinden küçük olmalıdır.");
  }

  return {
    W1: { start: W1_START, end: W1_END },
    W2: { start: W2_START, end: W2_END },
  };
}
