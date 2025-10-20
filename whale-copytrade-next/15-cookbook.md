# Cookbook (TS/Next/viem)

## Zaman→Blok
- `getBlockNumber`, `getBlock` (viem) ile binary search.

## Swap Log Tarama
- V2 Pair ve V3 Pool `Swap` topic0 ile `getLogs`.
- `Interface/ABI` ile decode → token0/token1 ve amount'lar.
- Trader: tx.from (çoğunlukla taker); ek heuristik: pool->to/recipient.

## CEX/Bridge Ayıklama
- `labels` tablosu join; eşleşirse exclude.

## Skor + CSV
- `scores` tablosu doldur; `out/candidates.csv` üret.

