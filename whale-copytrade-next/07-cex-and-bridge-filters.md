# CEX & Bridge Filtreleri (etiket yaklaşımı)

- `labels` tablosuna aşağıdaki CSV'ler yüklenecek:
  - `labels/cex.csv`  → address,labelValue (ör. binance, coinbase, kraken …)
  - `labels/bridge.csv` → bridge kontratları (stargate, across, hop …)
  - `labels/lp.csv` → add/remove liquidity kontratları (isteğe bağlı)

Filtreleme Kuralları:
- `to` veya `from` CEX etiketli adres ise → "CEX" olarak işaretle ve **trade analizinden çıkar**.
- Bridge kontrat çağrıları → "bridge" etiketi, **hariç**.
- LP add/remove event'leri → **hariç**.

> İlk tohum listeleri az olabilir; zamanla genişlet. Çekirdek akış, etiket tabloları üzerinden idempotent çalışır.

