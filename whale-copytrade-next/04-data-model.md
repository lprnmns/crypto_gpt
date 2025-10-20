# Veri Modeli (Prisma + PostgreSQL)

## Mantıksal Şema
- chains(id, name, httpRpc, wsRpc)
- blocks(chainId, number, timestamp)
- tokens(chainId, address, symbol, decimals)
- swaps(id, chainId, txHash, logIndex, blockNumber, timestamp,
        pool, router, trader, tokenIn, amountInRaw, tokenOut, amountOutRaw,
        usdIn, usdOut, usdNotional, dex, viaAggregator BOOLEAN)
- labels(address, labelType, labelValue)  -- cex/binance, bridge/stargate, lp, vb.
- wallets(address, firstSeen, tradeRatio, notes)
- pnl(wallet, day, realizedUsd, gasUsd, netUsd)
- scores(wallet, asOf, pnlT1, pnlT7, winrate, tradeRatio, slippageAvgBps, score)

## Prisma (örnek `schema.prisma` parçacığı)
```prisma
model Swap {
  id            BigInt   @id @default(autoincrement())
  chainId       Int
  txHash        String
  logIndex      Int
  blockNumber   BigInt
  timestamp     DateTime
  pool          String
  router        String?
  trader        String
  tokenIn       String
  amountInRaw   String    // decimal için string
  tokenOut      String
  amountOutRaw  String
  usdIn         Decimal?  @db.Decimal(38, 18)
  usdOut        Decimal?  @db.Decimal(38, 18)
  usdNotional   Decimal?  @db.Decimal(38, 18)
  dex           String?
  viaAggregator Boolean   @default(false)

  @@index([chainId, timestamp])
  @@index([trader])
}
```

Not: Büyük sayılar için Decimal/String tercih edin; normalizasyon UI katmanında yapılır.

