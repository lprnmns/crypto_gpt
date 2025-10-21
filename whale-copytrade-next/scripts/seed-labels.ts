import "dotenv/config";
import { readFileSync, readdirSync } from "node:fs";
import { join, parse } from "node:path";
import { prisma } from "@/db/client";

interface CsvRecord {
  address: string;
  labelValue: string;
}

function parseCsv(content: string): CsvRecord[] {
  const lines = content
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);

  if (lines.length <= 1) {
    return [];
  }

  const [, ...rows] = lines;

  return rows
    .map((row) => {
      const [address, labelValue] = row.split(",").map((value) => value.trim());
      if (!address) {
        return null;
      }
      return {
        address: address.toLowerCase(),
        labelValue: labelValue ?? "",
      };
    })
    .filter((value): value is CsvRecord => value !== null);
}

async function seedFile(filePath: string, labelType: string) {
  const content = readFileSync(filePath, "utf8");
  const records = parseCsv(content);
  if (records.length === 0) {
    console.warn(`No records found in ${filePath}, skipping.`);
    return;
  }

  for (const record of records) {
    await prisma.label.upsert({
      where: {
        labelType_address: {
          labelType,
          address: record.address,
        },
      },
      update: {
        labelValue: record.labelValue,
      },
      create: {
        labelType,
        address: record.address,
        labelValue: record.labelValue,
      },
    });
  }

  console.log(`Seeded ${records.length} records for ${labelType}.`);
}

async function main() {
  const labelsDir = join(process.cwd(), "labels");
  const files = readdirSync(labelsDir);

  for (const file of files) {
    if (!file.endsWith(".csv")) continue;
    const { name } = parse(file); // e.g. cex, bridge, lp
    await seedFile(join(labelsDir, file), name);
  }
}

main()
  .then(async () => {
    await prisma.$disconnect();
    console.log("Label seeding completed.");
  })
  .catch(async (error) => {
    console.error("Label seeding failed:", error);
    await prisma.$disconnect();
    process.exit(1);
  });

