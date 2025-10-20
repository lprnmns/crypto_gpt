# Whale Copytrade (Next.js + TypeScript + PostgreSQL)

Amaç: 10 Ekim 2025 flash crash (19:00–23:00 UTC) penceresini "ground truth" olarak kullanıp,
W1 (19–21) net satış + W2 (21–23) net alış paterni gösteren, işlemleri **on‑chain DEX `Swap`** olan
**balina cüzdanları** bulmak ve bunları gerçek zamanlı izleyerek sinyal üretmek.

Yığın (tamamen ücretsiz katmanlarla):
- **Next.js 14+ (TypeScript, App Router)** – UI + API Routes
- **PostgreSQL** (Docker Compose, yerel)
- **Prisma ORM**
- **viem** (JSON‑RPC/WebSocket, EVM)
- **Zod** (API sözleşmesi/validasyon)
- **Tailwind** (opsiyonel UI)

Çekirdek akış: public RPC + log tarama + açık kaynak subgraph (opsiyonel) + yerel Postgres.

