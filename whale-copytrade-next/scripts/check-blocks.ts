import "dotenv/config";
import { getWindowBlocks } from "@/lib/blocks";

async function main() {
  const result = await getWindowBlocks({
    chain: "eth",
    fromIso: "2025-10-10T19:00:00Z",
    toIso: "2025-10-10T21:00:00Z",
  });

  console.log(result);
}

main().catch((error) => {
  console.error("Hata:", error);
  process.exit(1);
});

