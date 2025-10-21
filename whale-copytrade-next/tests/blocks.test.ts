import { beforeEach, describe, expect, it, vi } from "vitest";

const mockGetPublicClient = vi.fn();

vi.mock("@/lib/rpc", () => ({
  getPublicClient: mockGetPublicClient,
  withRetry: async <T>(fn: () => Promise<T>) => fn(),
}));

describe("blocks", () => {
  beforeEach(() => {
    vi.resetModules();
    mockGetPublicClient.mockReset();
  });

  it("performs binary search for block at timestamp", async () => {
    mockGetPublicClient.mockReturnValue({
      getBlockNumber: vi.fn(async () => 100n),
      getBlock: vi.fn(async ({ blockNumber }: { blockNumber: bigint }) => ({
        number: blockNumber,
        timestamp: blockNumber * 10n,
      })),
    });

    const { getBlockAtOrAfter } = await import("@/lib/blocks");

    const result = await getBlockAtOrAfter("eth", "1970-01-01T00:05:00Z");
    expect(result.number).toBe(30n);
    expect(result.timestamp).toBe(300n);
  });
});

