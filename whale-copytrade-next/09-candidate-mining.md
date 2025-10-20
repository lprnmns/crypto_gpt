# Aday Madenciliği (Crash Penceresi)

Girdi:
- W1: 19:00–21:00 UTC (de‑risk)
- W2: 21:00–23:00 UTC (re‑risk)
- Eşik: USD_MIN=$30k

Adımlar:
1) Zincir × pencere blok aralığı → `Swap` loglarını topla.
2) Decimals normalize → USD≈ notional çıkar.
3) CEX/bridge/LP ayıklaması (etiket tabloları).
4) Cüzdan bazında net akış:
   - W1: majör→stable (+) → **net satış**
   - W2: stable→majör (+) → **net alış**
5) İki pencerede de eşik üstü hacim ve patern eşleşmesi → **aday**.

