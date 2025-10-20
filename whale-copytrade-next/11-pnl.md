# PnL Metodolojisi

Eşleme:
- FIFO varsayılan; LIFO opsiyon.
- Her swap → DEX fee + gas dahil edilir.

Fiyatlama:
- İşlem anı USD ≈ Chainlink feed (varsa) veya UniV3 TWAP.
- Kapanış değerleri için aynı kaynaklardan "yakın an" ölçümü.

Çıktı:
- T+1, T+7 realize/net PnL (USD)
- Gün sonu net varlık değeri (USD normalize)

