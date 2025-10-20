# Agent İşletim Prosedürü (TS/Next/Postgres)

1) `.env` ve `docker-compose up -d` ile Postgres'i başlat.
2) Prisma migrate → tablo yapısı kur.
3) `/api/blocks` ile W1/W2 blok aralıklarını üret ve `blocks`'a yaz.
4) `/api/collect` ile Uni V2/V3 `Swap` loglarını topla → DB.
5) `/api/price` ile USD normalize et.
6) `/api/mine` ile patern eşleşmesini yap → aday tabloları.
7) `/api/score` ile skorları hesapla → `out/candidates.csv`.
8) `scripts/worker.ts` ile watch (dry‑run).
9) `/api/report` ile `out/report.md` oluştur; UI'da göster.

Rapor:
- Özet metrikler, en iyi 20 tablo, örnek trade zinciri linkleri.

