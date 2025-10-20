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

