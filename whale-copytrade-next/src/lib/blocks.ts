import { getPublicClient, withRetry } from "@/lib/rpc";
import type { Chain } from "@/lib/types";

export interface BlockWindowInput {
  chain: Chain;
  fromIso: string;
  toIso: string;
}

export interface BlockWindowResult {
  fromBlock: string;
  toBlock: string;
  tookMs: number;
}

interface LocatedBlock {
  number: bigint;
  timestamp: bigint;
}

function isoToUnixSeconds(iso: string): bigint {
  const parsed = Date.parse(iso);
  if (Number.isNaN(parsed)) {
    throw new Error(`Geçersiz ISO tarihi: ${iso}`);
  }
  return BigInt(Math.floor(parsed / 1000));
}

async function fetchBlock(chain: Chain, blockNumber: bigint) {
  const client = getPublicClient(chain);
  return withRetry(() =>
    client.getBlock({
      blockNumber,
    }),
  );
}

async function getLatest(chain: Chain): Promise<LocatedBlock> {
  const client = getPublicClient(chain);
  const latestNumber = await withRetry(() => client.getBlockNumber());
  const latestBlock = await fetchBlock(chain, latestNumber);
  return { number: latestBlock.number, timestamp: latestBlock.timestamp };
}

export async function getBlockAtOrAfter(
  chain: Chain,
  isoTimestamp: string,
): Promise<LocatedBlock> {
  const targetTs = isoToUnixSeconds(isoTimestamp);
  const latest = await getLatest(chain);

  if (targetTs >= latest.timestamp) {
    return latest;
  }

  let low = 0n;
  let high = latest.number;
  let candidate: LocatedBlock = latest;
  let iterations = 0;

  while (low <= high && iterations < 80) {
    const mid = (low + high) / 2n;
    const block = await fetchBlock(chain, mid);
    iterations += 1;

    if (block.timestamp < targetTs) {
      low = mid + 1n;
    } else {
      candidate = { number: block.number, timestamp: block.timestamp };
      if (mid === 0n) break;
      high = mid - 1n;
    }
  }

  return candidate;
}

export async function getWindowBlocks({
  chain,
  fromIso,
  toIso,
}: BlockWindowInput): Promise<BlockWindowResult> {
  const started = Date.now();
  const fromSeconds = isoToUnixSeconds(fromIso);
  const toSeconds = isoToUnixSeconds(toIso);

  if (fromSeconds > toSeconds) {
    throw new Error("fromIso, toIso'dan büyük olamaz.");
  }

  const [fromBlock, toBlock] = await Promise.all([
    getBlockAtOrAfter(chain, fromIso),
    getBlockAtOrAfter(chain, toIso),
  ]);

  return {
    fromBlock: fromBlock.number.toString(),
    toBlock: toBlock.number.toString(),
    tookMs: Date.now() - started,
  };
}

