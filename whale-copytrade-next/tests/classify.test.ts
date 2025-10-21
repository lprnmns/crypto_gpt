import { beforeEach, describe, expect, it, vi } from "vitest";

const findMany = vi.fn();

vi.mock("@/db/client", () => ({
  prisma: {
    label: {
      findMany,
    },
  },
}));

describe("classify helpers", () => {
  beforeEach(() => {
    vi.resetModules();
    findMany.mockReset();
  });

  it("builds label map and detects exclusions", async () => {
    findMany.mockResolvedValue([
      {
        address: "0xabc",
        labelType: "cex",
        labelValue: "binance",
      },
      {
        address: "0xdef",
        labelType: "bridge",
        labelValue: "across",
      },
    ]);

    const { getLabelMap, hasExclusionLabel } = await import("@/lib/classify");

    const map = await getLabelMap([
      "0xAbc",
      "0xdef",
      "0x123",
    ]);

    expect(map.size).toBe(2);
    expect(hasExclusionLabel("0xabc", map)).toBe(true);
    expect(hasExclusionLabel("0xDEF", map)).toBe(true);
    expect(hasExclusionLabel("0x123", map)).toBe(false);
  });
});

