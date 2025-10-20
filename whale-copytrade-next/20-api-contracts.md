# API Sözleşmeleri (Zod + JSON)

## /api/blocks (POST)
Body: { chain: "eth"|"arb"|"base", fromIso: string, toIso: string }
Resp: { fromBlock: string, toBlock: string, tookMs: number }

## /api/collect (POST)
Body: { chain, fromBlock, toBlock, usdMin?: number }
Resp: { inserted: number, skipped: number }

## /api/price (POST)
Body: { chain, txHash?: string, fromBlock?: string, toBlock?: string }
Resp: { priced: number, missing: number }

## /api/mine (POST)
Body: { window: "W1"|"W2"|"both", usdMin?: number }
Resp: { candidates: number }

## /api/score (POST)
Body: { lookbackDays?: number }
Resp: { updated: number, top: Array<{ wallet, score }> }

## /api/candidates (GET)
Query: { limit?: number, sort?: "score_desc"|... }
Resp: { items: [...], total: number }

