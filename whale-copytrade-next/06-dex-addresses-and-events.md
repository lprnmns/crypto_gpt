# DEX Adresleri & Event İmzaları (TS)

## Uniswap V2
- Pair sözleşmelerindeki Event:
  `Swap(address indexed sender, uint amount0In, uint amount1In, uint amount0Out, uint amount1Out, address indexed to)`
- Topic0: keccak256("Swap(address,uint256,uint256,uint256,uint256,address)")
- Tarama: **Pair** adresleri + `Swap` topic0 → `token0/token1` ve `amount` alanlarıyla in/out çıkarılır.

## Uniswap V3
- Pool Event:
  `Swap(address indexed sender, address indexed recipient, int256 amount0, int256 amount1, uint160 sqrtPriceX96, uint128 liquidity, int24 tick)`
- Tarama: **Pool** sözleşmelerinde `Swap` topic0.

## Router/Aggregator
- 1inch/0x/CoW gibi aggregator çağrıları genelde Uni V2/V3 pool/pair'lerde iz bırakır.
- `router` alanını `tx.to` ile, `viaAggregator` bayrağını heuristikle işaretle.

## TS/viem ABIs
- `src/abi/univ2.ts` ve `src/abi/univ3.ts` içinde `Swap` event abi'lerini tanımla.
- `viem` ile `getLogs` ve WS `watchEventLogs` kullan.

