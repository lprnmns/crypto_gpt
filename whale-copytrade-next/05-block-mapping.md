# Zaman → Blok (Binary Search, TS)

Yöntem:
1) `eth_blockNumber` → high.
2) `low=genesis(~1)`; orta = (low+high)/2 → `eth_getBlockByNumber`.
3) Orta timestamp hedef < ise low=orta+1; değilse high=orta-1; yakınsama.

Uygulama:
- zincir başına `getBlockAtOrAfter(timestamp)` fonksiyonu.
- ±60 sn tolerans; pencere başı/sonu için ayrı hesapla.
- Sonuçları `blocks` tablosuna cache'le (yeniden kullan).

