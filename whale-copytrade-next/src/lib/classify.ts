import { prisma } from "@/db/client";

export type ExclusionLabel = "cex" | "bridge" | "lp";

export interface LabelMatch {
  labelType: string;
  labelValue: string;
}

export type LabelMap = Map<string, LabelMatch[]>;

const EXCLUDE_TYPES: ExclusionLabel[] = ["cex", "bridge", "lp"];

export async function getLabelMap(
  addresses: string[],
  types: string[] = EXCLUDE_TYPES,
): Promise<LabelMap> {
  if (addresses.length === 0) {
    return new Map();
  }

  const normalized = Array.from(
    new Set(addresses.map((address) => address.toLowerCase())),
  );

  const rows = await prisma.label.findMany({
    where: {
      address: {
        in: normalized,
      },
      labelType: {
        in: types,
      },
    },
  });

  const map: LabelMap = new Map();

  for (const row of rows) {
    const key = row.address.toLowerCase();
    const entry = map.get(key) ?? [];
    entry.push({ labelType: row.labelType, labelValue: row.labelValue });
    map.set(key, entry);
  }

  return map;
}

export function hasExclusionLabel(
  address: string | undefined,
  labelMap: LabelMap,
): boolean {
  if (!address) return false;
  const labels = labelMap.get(address.toLowerCase());
  return Boolean(labels && labels.length > 0);
}

