import "dotenv/config";
import { prisma } from "@/db/client";

async function main() {
  await prisma.chain.upsert({
    where: { id: 1 },
    update: {},
    create: { id: 1, name: "Ethereum" },
  });

  await prisma.chain.upsert({
    where: { id: 42161 },
    update: {},
    create: { id: 42161, name: "Arbitrum" },
  });

  await prisma.chain.upsert({
    where: { id: 8453 },
    update: {},
    create: { id: 8453, name: "Base" },
  });
}

main()
  .then(async () => {
    await prisma.$disconnect();
    console.log("Seed başarıyla tamamlandı.");
  })
  .catch(async (error) => {
    console.error(error);
    await prisma.$disconnect();
    process.exit(1);
  });

