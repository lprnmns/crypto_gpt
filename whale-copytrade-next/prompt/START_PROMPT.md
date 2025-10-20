SEN BİR KOD AJANISIN. AŞAĞIDAKİ BELGELERLE **TAMAMEN ÜCRETSİZ** BİR SİSTEMİ
**TYPESCRIPT + NEXT.JS + POSTGRESQL** ÜZERİNDE KURACAKSIN.

### STACK & KURULUM
- Next.js 14+ (TypeScript, App Router), pnpm.
- PostgreSQL (Docker Compose, yerel). Prisma ORM.
- viem (JSON‑RPC/WebSocket), abitype, zod, dayjs, decimal.js, chart.js, tailwind.
- Kod kalitesi: eslint, prettier, vitest (temel).
- Ücretli hiçbir servis kullanma; sadece free/public RPC/WS.

### GÖREVLER (DOSYALARI REFERANS AL)
Belgeler proje kökünde: `00-roadmap.md` … `22-dev-commands.md`.
Bunlara göre aşağıdaki çıktıları üret:

1) **Proje İskele**
   - Next.js TS app (App Router).
   - `src/lib` (rpc, logs, pricing, pnl, score), `src/abi` (univ2/univ3), `src/db` (prisma client).
   - `scripts/worker.ts` (WS watcher), `scripts/cli.ts` (CLI altkomutlar).
   - `labels/cex.csv`, `labels/bridge.csv`, `labels/lp.csv` (boş/tohum).
   - `out/` klasörü (raporlar, csv).
   - `docker-compose.yml` (postgres:16‑alpine, volume).
   - `.env.example` (bkz. `03-env.md`).

2) **Prisma Şeması & Migrasyon**
   - `schema.prisma` → `04-data-model.md`’deki modeli yansıt.
   - `pnpm prisma:migrate dev -n init` ile migrasyon scriptleri.

3) **RPC/WS Bağlantı Katmanı (viem)**
   - `getPublicClient(chain)` (HTTP) ve `getWsClient(chain)` (WS).
   - Rate‑limit/backoff; `getLogs` ve `watchEventLogs` yardımcıları.

4) **Zaman→Blok (Binary Search)**
   - `src/lib/blocks.ts`: `getBlockAtOrAfter(chain, isoTs)` ve `getWindowBlocks(chain, fromIso, toIso)`.
   - `/api/blocks` route handler (POST): zod ile body doğrulaması, DB cache.

5) **DEX Log Toplama**
   - `src/abi/univ2.ts` ve `src/abi/univ3.ts`’de `Swap` event ABIs.
   - `src/lib/collect.ts`: verilen blok aralığında `Swap` loglarını çek, decode et, normalize et.
   - Trader heuristiği: tx.from (default); v3 recipient/to alanı yardımcı sinyal.
   - `/api/collect` (POST): DB’ye `swaps` insert; `USD_MIN` filtresi opsiyonu.

6) **Etiket Filtreleri**
   - `labels/*` CSV’lerini DB `labels` tablosuna yükleyen seed script.
   - `src/lib/classify.ts`: CEX/bridge/LP eşleşmesi → exclude bayrakları.
   - Filtreleme `collect` sonrasında uygulanır.

7) **Fiyatlama & PnL**
   - `src/lib/price.ts`: Chainlink/UniV3 TWAP okuyup USD≈ hesapla; fallback referans parite.
   - `src/lib/pnl.ts`: FIFO (varsayılan) / LIFO (opsiyon); gas/fee dahil.
   - `/api/price` & `/api/score` route’ları.

8) **Aday Madenciliği & Skor**
   - `src/lib/mine.ts`: `09-candidate-mining.md` patern kontrolü.
   - `src/lib/score.ts`: `10-scorer.md` metrikleri.
   - `/api/mine` ve `/api/score` endpoint’leri.
   - `candidates.csv` çıktısı.

9) **Watch (Canlı)**
   - `scripts/worker.ts`: Seçili cüzdanları WS ile dinle, yeni `Swap`’ta DB insert + konsol “signal” bas (dry‑run).
   - `pnpm worker:watch` komutu.
   - `/api/watch` (opsiyonel): start/stop tetikleyicisi.

10) **UI**
   - **Dashboard**: metrik kartları + mini grafikler.
   - **Candidates**: tablo (wallet, score, pnlT1/T7, tradeRatio, chains).
   - **Wallet/[address]**: zaman çizelgesi, son swap’lar, W1/W2 davranışı.
   - **Settings**: RPC/parametreler (client-side only; .env server tarafı kritik).
   - Tailwind ile sade arayüz; explorer linkleri.

11) **CLI**
   - `pnpm cli blocks|collect|price|mine|score|report`
   - `report` → `out/report.md` üretir.

12) **Komutlar & Dokümantasyon**
   - `22-dev-commands.md`’deki komutları `package.json` script’lerine ekle:
     - `dev`, `build`, `start`, `cli`, `worker:watch`, `prisma:generate`, `prisma:migrate`.

13) **Testler**
   - vitest ile en az: blocks binary search, log decode, classifier filtre testleri.

### KABUL KRİTERLERİ
- `docker compose up -d` ile Postgres açılır, `pnpm prisma:migrate` başarıyla çalışır.
- `/api/blocks` W1/W2 için mantıklı blok aralıkları döndürür (±1dk).
- `pnpm cli collect --chains eth,arb,base --w W1,W2` en az 10k `Swap` logunu işler.
- `pnpm cli mine` sonrası ≥200 benzersiz aday.
- `pnpm cli score` sonrası `out/candidates.csv` (≥20 satır, skor azalan).
- `pnpm worker:watch` 5 dk içinde en az 1 sinyal loglar (dry‑run).
- UI “Candidates” sayfası en iyi 20’yi gösterir.

### KISITLAR
- Ücretli hiçbir API kullanma; yalnızca public/free RPC/WS.
- Tüm hassas anahtarlar `.env` ve server tarafında kalsın.
- Büyük sayıları `Decimal/String` olarak işle; overflow’a dikkat.

Başla.
