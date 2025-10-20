-- CreateTable
CREATE TABLE "Chain" (
    "id" INTEGER NOT NULL,
    "name" TEXT NOT NULL,
    "httpRpc" TEXT,
    "wsRpc" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "Chain_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Block" (
    "chainId" INTEGER NOT NULL,
    "number" BIGINT NOT NULL,
    "timestamp" TIMESTAMP(3) NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "Block_pkey" PRIMARY KEY ("chainId","number")
);

-- CreateTable
CREATE TABLE "Token" (
    "chainId" INTEGER NOT NULL,
    "address" TEXT NOT NULL,
    "symbol" TEXT NOT NULL,
    "decimals" INTEGER NOT NULL,
    "name" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "Token_pkey" PRIMARY KEY ("chainId","address")
);

-- CreateTable
CREATE TABLE "Swap" (
    "id" BIGSERIAL NOT NULL,
    "chainId" INTEGER NOT NULL,
    "txHash" TEXT NOT NULL,
    "logIndex" INTEGER NOT NULL,
    "blockNumber" BIGINT NOT NULL,
    "timestamp" TIMESTAMP(3) NOT NULL,
    "pool" TEXT NOT NULL,
    "router" TEXT,
    "trader" TEXT NOT NULL,
    "tokenIn" TEXT NOT NULL,
    "amountInRaw" TEXT NOT NULL,
    "tokenOut" TEXT NOT NULL,
    "amountOutRaw" TEXT NOT NULL,
    "usdIn" DECIMAL(38,18),
    "usdOut" DECIMAL(38,18),
    "usdNotional" DECIMAL(38,18),
    "dex" TEXT,
    "viaAggregator" BOOLEAN NOT NULL DEFAULT false,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "Swap_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Label" (
    "id" BIGSERIAL NOT NULL,
    "address" TEXT NOT NULL,
    "labelType" TEXT NOT NULL,
    "labelValue" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "Label_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Wallet" (
    "address" TEXT NOT NULL,
    "firstSeen" TIMESTAMP(3),
    "tradeRatio" DECIMAL(20,8),
    "notes" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "Wallet_pkey" PRIMARY KEY ("address")
);

-- CreateTable
CREATE TABLE "Pnl" (
    "id" BIGSERIAL NOT NULL,
    "walletAddress" TEXT NOT NULL,
    "day" TIMESTAMP(3) NOT NULL,
    "realizedUsd" DECIMAL(38,18) NOT NULL,
    "gasUsd" DECIMAL(38,18),
    "netUsd" DECIMAL(38,18),
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "Pnl_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Score" (
    "id" BIGSERIAL NOT NULL,
    "walletAddress" TEXT NOT NULL,
    "asOf" TIMESTAMP(3) NOT NULL,
    "pnlT1" DECIMAL(38,18),
    "pnlT7" DECIMAL(38,18),
    "winrate" DECIMAL(10,6),
    "tradeRatio" DECIMAL(10,6),
    "slippageAvgBps" DECIMAL(10,6),
    "repeatability" DECIMAL(10,6),
    "liquidityQuality" DECIMAL(10,6),
    "score" DECIMAL(10,6),
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "Score_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE INDEX "Block_chainId_timestamp_idx" ON "Block"("chainId", "timestamp");

-- CreateIndex
CREATE INDEX "Token_symbol_idx" ON "Token"("symbol");

-- CreateIndex
CREATE INDEX "Swap_chainId_timestamp_idx" ON "Swap"("chainId", "timestamp");

-- CreateIndex
CREATE INDEX "Swap_trader_idx" ON "Swap"("trader");

-- CreateIndex
CREATE UNIQUE INDEX "Swap_chainId_txHash_logIndex_key" ON "Swap"("chainId", "txHash", "logIndex");

-- CreateIndex
CREATE INDEX "Label_labelValue_idx" ON "Label"("labelValue");

-- CreateIndex
CREATE UNIQUE INDEX "Label_labelType_address_key" ON "Label"("labelType", "address");

-- CreateIndex
CREATE UNIQUE INDEX "Pnl_walletAddress_day_key" ON "Pnl"("walletAddress", "day");

-- CreateIndex
CREATE INDEX "Score_score_idx" ON "Score"("score");

-- CreateIndex
CREATE UNIQUE INDEX "Score_walletAddress_asOf_key" ON "Score"("walletAddress", "asOf");

-- AddForeignKey
ALTER TABLE "Block" ADD CONSTRAINT "Block_chainId_fkey" FOREIGN KEY ("chainId") REFERENCES "Chain"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Token" ADD CONSTRAINT "Token_chainId_fkey" FOREIGN KEY ("chainId") REFERENCES "Chain"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Swap" ADD CONSTRAINT "Swap_chainId_fkey" FOREIGN KEY ("chainId") REFERENCES "Chain"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Pnl" ADD CONSTRAINT "Pnl_walletAddress_fkey" FOREIGN KEY ("walletAddress") REFERENCES "Wallet"("address") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Score" ADD CONSTRAINT "Score_walletAddress_fkey" FOREIGN KEY ("walletAddress") REFERENCES "Wallet"("address") ON DELETE CASCADE ON UPDATE CASCADE;
