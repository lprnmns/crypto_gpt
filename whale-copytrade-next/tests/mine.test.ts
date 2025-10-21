import { beforeEach, describe, expect, it, vi } from "vitest";

const findMany = vi.fn();

vi.mock("@/db/client", () => ({
  prisma: {
    swap: {
      findMany,
    },
  },
}));

const getUsdMinMock = vi.fn(() => 30000);
const getMiningWindowsMock = vi.fn();

vi.mock("@/lib/env", async () => {
  const actual = await vi.importActual<typeof import("@/lib/env")>("@/lib/env");
  return {
    ...actual,
    getUsdMin: getUsdMinMock,
    getMiningWindows: getMiningWindowsMock,
  };
});

describe("mineCandidates", () => {
  beforeEach(() => {
    vi.resetModules();
    findMany.mockReset();
    getUsdMinMock.mockReset();
    getMiningWindowsMock.mockReset();
    getUsdMinMock.mockReturnValue(1000);
    getMiningWindowsMock.mockReturnValue({
      W1: { start: new Date("2025-10-10T19:00:00Z"), end: new Date("2025-10-10T21:00:00Z") },
      W2: { start: new Date("2025-10-10T21:00:00Z"), end: new Date("2025-10-10T23:00:00Z") },
    });
  });

  it("finds wallets with opposite net flows", async () => {
    findMany
      .mockResolvedValueOnce([
        {
          trader: "0xWallet1",
          chainId: 1,
          usdIn: null,
          usdOut: { toNumber: () => 1500 },
          usdNotional: { toNumber: () => 1500 },
        },
      ])
      .mockResolvedValueOnce([
        {
          trader: "0xWallet1",
          chainId: 1,
          usdIn: { toNumber: () => 2000 },
          usdOut: null,
          usdNotional: { toNumber: () => 2000 },
        },
      ]);

    const { mineCandidates } = await import("@/lib/mine");
    const result = await mineCandidates();

    expect(result).toHaveLength(1);
    expect(result[0].wallet).toBe("0xwallet1");
    expect(result[0].w1Net).toBeCloseTo(1500);
    expect(result[0].w2Net).toBeCloseTo(2000);
  });

  it("filters wallets below USD threshold", async () => {
    findMany.mockResolvedValueOnce([
      {
        trader: "0xWallet2",
        chainId: 1,
        usdIn: null,
        usdOut: { toNumber: () => 500 },
        usdNotional: { toNumber: () => 500 },
      },
    ]);
    findMany.mockResolvedValueOnce([
      {
        trader: "0xWallet2",
        chainId: 1,
        usdIn: { toNumber: () => 400 },
        usdOut: null,
        usdNotional: { toNumber: () => 400 },
      },
    ]);

    const { mineCandidates } = await import("@/lib/mine");
    const result = await mineCandidates();

    expect(result).toHaveLength(0);
  });
});

