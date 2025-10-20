export interface SwapLog {
  chainId: number;
  blockNumber: bigint;
  blockTimestamp: Date;
  txHash: string;
  logIndex: number;
  pool: string;
  trader: string;
  router?: string;
  tokenIn: string;
  amountInRaw: bigint;
  tokenOut: string;
  amountOutRaw: bigint;
  dex: "univ2" | "univ3";
  viaAggregator: boolean;
}

