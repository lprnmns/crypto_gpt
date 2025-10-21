import { beforeEach, describe, expect, it, vi } from "vitest";

const swapFindMany = vi.fn();
const swapUpdate = vi.fn();
const swapDeleteMany = vi.fn();
const transaction = vi.fn(async (operations: Promise<unknown>[]) =>
  Promise.all(operations),
);
const readContract = vi.fn();

vi.mock("@/db/client", () => ({
  prisma: {
    swap: {
      findMany: swapFindMany,
      update: swapUpdate,
      deleteMany: swapDeleteMany,
    },
    $transaction: transaction,
  },
}));

vi.mock("@/lib/tokens", () => ({
  getTokenMetadata: vi.fn(async (_chain: string, address: string) => ({
    address,
    symbol: address.endsWith("c7") ? "USDC" : "USDT",
    decimals: 6,
  })),
}));

vi.mock("@/lib/rpc", () => ({
  getPublicClient: () => ({
    readContract,
  }),
}));

const getUsdMinMock = vi.fn(() => 0);

vi.mock("@/lib/env", () => ({
  getUsdMin: getUsdMinMock,
}));

describe("priceSwaps", () => {
  beforeEach(() => {
    vi.resetModules();
    swapFindMany.mockReset();
    swapUpdate.mockReset();
    swapDeleteMany.mockReset();
    transaction.mockReset();
    getUsdMinMock.mockReset();
    getUsdMinMock.mockReturnValue(0);
    swapDeleteMany.mockResolvedValue({ count: 0 });
    readContract.mockReset();
  });

  it("prices stablecoin swaps and leaves summary", async () => {
    swapFindMany.mockResolvedValue([
      {
        id: 1n,
        chainId: 1,
        tokenIn: "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48",
        amountInRaw: "1000000",
        tokenOut: "0xdac17f958d2ee523a2206206994597c13d831ec7",
        amountOutRaw: "2000000",
        blockNumber: 100n,
        logIndex: 0,
        timestamp: new Date(),
        pool: "",
        router: null,
        trader: "",
        usdIn: null,
        usdOut: null,
        usdNotional: null,
        dex: "univ2",
        viaAggregator: false,
        txHash: "",
      },
    ]);
    swapUpdate.mockResolvedValue({});

    const { priceSwaps } = await import("@/lib/price");

    const result = await priceSwaps({ chain: "eth" });

    expect(result).toEqual({
      priced: 1,
      missing: 0,
      filtered: 0,
    });
    expect(transaction).toHaveBeenCalledTimes(1);
    expect(swapDeleteMany).not.toHaveBeenCalled();
  });

  it("returns empty summary when no swaps", async () => {
    swapFindMany.mockResolvedValue([]);
    const { priceSwaps } = await import("@/lib/price");
    const result = await priceSwaps({ chain: "eth" });

    expect(result).toEqual({ priced: 0, missing: 0, filtered: 0 });
  });

  it("filters swaps below usdMin", async () => {
    getUsdMinMock.mockReturnValue(10);
    swapFindMany.mockResolvedValue([
      {
        id: 2n,
        chainId: 1,
        tokenIn: "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48",
        amountInRaw: "1000000",
        tokenOut: "0xdac17f958d2ee523a2206206994597c13d831ec7",
        amountOutRaw: "1000000",
        blockNumber: 100n,
        logIndex: 0,
        timestamp: new Date(),
        pool: "",
        router: null,
        trader: "",
        usdIn: null,
        usdOut: null,
        usdNotional: null,
        dex: "univ2",
        viaAggregator: false,
        txHash: "",
      },
    ]);
    swapDeleteMany.mockResolvedValue({ count: 1 });

    const { priceSwaps } = await import("@/lib/price");
    const result = await priceSwaps({ chain: "eth" });

    expect(result).toEqual({ priced: 0, missing: 0, filtered: 1 });
    expect(swapUpdate).not.toHaveBeenCalled();
    expect(swapDeleteMany).toHaveBeenCalled();
  });
});
