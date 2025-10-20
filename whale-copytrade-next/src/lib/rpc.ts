import {
  createClient,
  createPublicClient,
  fallback,
  http,
  webSocket,
} from "viem";
import { arbitrum, base, mainnet } from "viem/chains";
import { getRpcUrl } from "@/lib/env";
import type { Chain } from "@/lib/types";

const viemChains = {
  eth: mainnet,
  arb: arbitrum,
  base,
} as const;

const httpClients = new Map<Chain, ReturnType<typeof createPublicClient>>();
const wsClients = new Map<Chain, ReturnType<typeof createClient>>();

export function getPublicClient(chain: Chain) {
  if (!httpClients.has(chain)) {
    const url = getRpcUrl(chain, "http");
    const client = createPublicClient(
      {
        chain: viemChains[chain],
        transport: fallback([http(url)], {
          retryCount: 0,
        }),
        batch: {
          multicall: true,
        },
      } as any,
    ) as ReturnType<typeof createPublicClient>;

    httpClients.set(chain, client);
  }

  return httpClients.get(chain)!;
}

export function getWsClient(chain: Chain) {
  if (!wsClients.has(chain)) {
    const url = getRpcUrl(chain, "ws");
    const client = createClient(
      {
        chain: viemChains[chain],
        transport: webSocket(url, {
          retryCount: 5,
          retryDelay: 1_000,
        }),
      } as any,
    ) as ReturnType<typeof createClient>;

    wsClients.set(chain, client);
  }

  return wsClients.get(chain)!;
}

interface RetryOptions {
  retries?: number;
  delayMs?: number;
  factor?: number;
}

export async function withRetry<T>(
  operation: () => Promise<T>,
  { retries = 3, delayMs = 250, factor = 2 }: RetryOptions = {},
): Promise<T> {
  let attempt = 0;
  let lastError: unknown;

  while (attempt <= retries) {
    try {
      return await operation();
    } catch (error) {
      lastError = error;
      if (attempt === retries) break;

      const jitter = Math.random() * 50;
      const waitFor = delayMs * factor ** attempt + jitter;
      await new Promise((resolve) => setTimeout(resolve, waitFor));
      attempt += 1;
    }
  }

  throw lastError;
}
