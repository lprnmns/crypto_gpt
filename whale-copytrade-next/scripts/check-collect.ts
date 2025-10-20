import "dotenv/config";
import { collectSwaps } from "@/lib/collect";

async function main() {
  const result = await collectSwaps({
    chain: "eth",
    fromBlock: 23549272n,
    toBlock: 23549290n,
  });

  console.log(`Toplam log: ${result.totalLogs}`);
  console.log("İlk 3 kayıt:", result.items.slice(0, 3));
}

main().catch((error) => {
  console.error("Hata:", error);
  process.exit(1);
});

