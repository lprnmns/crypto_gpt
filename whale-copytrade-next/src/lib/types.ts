export type Chain = "eth" | "arb" | "base";

export interface ChainMeta {
  id: number;
  name: string;
}

export const CHAINS: Record<Chain, ChainMeta> = {
  eth: { id: 1, name: "Ethereum" },
  arb: { id: 42161, name: "Arbitrum" },
  base: { id: 8453, name: "Base" },
};

