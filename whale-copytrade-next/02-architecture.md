# Mimari (App + Worker + DB)

**App (Next.js TS)**
- `/app` UI: Dashboard, Candidates, Wallet detail, Settings.
- `/app/api/*` Route Handlers:
  - `/api/blocks` → zaman→blok aralığı hesaplar (binary search).
  - `/api/collect` → blok aralığında `Swap` loglarını çeker, normalize eder.
  - `/api/price` → USD değeri hesaplar (Chainlink/UniV3 TWAP veya referans parite).
  - `/api/mine` → patern eşleşmesi (W1 sat, W2 al) → aday yaz.
  - `/api/score` → PnL/metric hesaplar → skorlar.
  - `/api/report` → özet rapor üretir.
  - `/api/watch` → (opsiyonel) izleme kontrolü başlat/durdur (in-proc worker tetikleyici).

**Worker (Node TS script)**
- `scripts/worker.ts`: WebSocket RPC dinler, yeni `Swap` yakalar, DB'ye yazar.
- CLI alt-komutları: `blocks`, `collect`, `price`, `mine`, `score`, `report`, `watch`.

**DB (PostgreSQL + Prisma)**
- Şema: cüzdan/işlem/token/etiket/PnL/score tabloları.
- Migration: Prisma migrate.

