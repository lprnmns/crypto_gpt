# Skorlama (Smart Trader Score)

Önerilen ağırlıklar:
- T+1 PnL (30%), T+7 PnL (20%)
- Win‑rate (10%)
- Trade Oranı (15%) = DEX trade / toplam tx
- Slippage/Etki (10%) = büyük işlemlerde fiyat kayması
- Tekrar Edilebilirlik (10%) = geçmiş vol epizotlarında benzer patern
- Likidite kalitesi (5%) = majör havuzlar/route kalitesi

Skor = normalize edilmiş metriklerin ağırlıklı toplamı.

