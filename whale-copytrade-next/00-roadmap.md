# Yol Haritası (TS/Next/Postgres)

## Sürüm 0 (Keşif, 1–2 gün)
- Zincirler: Ethereum, Arbitrum, Base.
- 10 Eki 2025 19–21 ve 21–23 UTC için **blok aralığı** (binary search).
- Uniswap V2/V3 `Swap` loglarını topla; USD≈ değeri hesapla; ≥$30k filtrele.
- CEX/bridge/LP ayıkla → on‑chain DEX trade kalır.
- "Önce sat, sonra al" paterni olan **aday cüzdanlar**.

## Sürüm 0.1 (Skor + Rapor, 1–2 gün)
- FIFO/LIFO realize PnL (T+1, T+7) + gas/fee.
- "Trade oranı", slippage tahmini, tekrar edilebilirlik → **Smart Trader Skoru**.
- En iyi 20 → `candidates.csv` + web dashboard.

## Sürüm 0.2 (Canlı, 2–3 gün)
- WebSocket RPC ile watch; yeni `Swap`'ta sinyal üret (dry‑run).
- Guardrails: allowlist token, max slippage, poz. limiti, soğuma.
- Günlük rapor (`report.md`) + UI grafikleri.
