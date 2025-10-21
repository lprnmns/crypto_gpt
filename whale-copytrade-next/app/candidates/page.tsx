import Link from "next/link";
import { getScoredCandidates } from "@/lib/analytics";

export const revalidate = 0;

function formatUsd(value: number) {
  return new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 0,
  }).format(value);
}

function formatNumber(value: number) {
  return new Intl.NumberFormat("tr-TR", {
    maximumFractionDigits: 0,
  }).format(value);
}

export default async function CandidatesPage() {
  const scores = await getScoredCandidates({
    chain: "all",
    mode: "fifo",
  });

  return (
    <main className="mx-auto flex min-h-screen w-full max-w-6xl flex-col gap-8 px-6 py-12">
      <header className="flex flex-col gap-2">
        <h1 className="text-3xl font-semibold text-slate-100">
          Whale Copytrade Skor Tablosu
        </h1>
        <p className="text-sm text-slate-400">
          W1 (T0) ve W2 (T1) pencerelerindeki net USD akışına göre aday cüzdanlar.
          Yeni veriler işlendiğinde tablo otomatik olarak güncellenir.
        </p>
      </header>

      <section className="overflow-hidden rounded-lg border border-slate-800 bg-slate-950/60 shadow-sm">
        <table className="min-w-full divide-y divide-slate-800 text-sm text-slate-200">
          <thead className="bg-slate-900/70 text-xs uppercase tracking-wide text-slate-400">
            <tr>
              <th className="px-4 py-3 text-left">Cüzdan</th>
              <th className="px-4 py-3 text-right">Skor</th>
              <th className="px-4 py-3 text-right">Gerçekleşen PnL</th>
              <th className="px-4 py-3 text-right">T0 Bakiyesi</th>
              <th className="px-4 py-3 text-right">T1 Bakiyesi</th>
              <th className="px-4 py-3 text-right">W1 Net</th>
              <th className="px-4 py-3 text-right">W2 Net</th>
              <th className="px-4 py-3 text-center">Zincirler</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800">
            {scores.length === 0 ? (
              <tr>
                <td
                  colSpan={8}
                  className="px-4 py-8 text-center text-sm text-slate-500"
                >
                  Henüz aday bulunamadı. /api/collect &amp; /api/score üzerinden
                  veri üretildiğinde tabloda belirecekler.
                </td>
              </tr>
            ) : (
              scores.map((item) => {
                const raw = item.metrics.raw;
                return (
                  <tr key={item.wallet} className="hover:bg-slate-900/60">
                    <td className="px-4 py-3 font-mono text-xs text-slate-300">
                      <Link
                        href={`https://etherscan.io/address/${item.wallet}`}
                        target="_blank"
                        className="underline decoration-slate-600 hover:text-sky-400"
                      >
                        {item.wallet}
                      </Link>
                    </td>
                    <td className="px-4 py-3 text-right text-slate-100">
                      {item.score.toFixed(3)}
                    </td>
                    <td className="px-4 py-3 text-right text-emerald-400">
                      {formatUsd(raw.realized)}
                    </td>
                    <td className="px-4 py-3 text-right text-slate-300">
                      {formatUsd(raw.w1Volume)}
                    </td>
                    <td className="px-4 py-3 text-right text-slate-300">
                      {formatUsd(raw.w2Volume)}
                    </td>
                    <td className="px-4 py-3 text-right text-slate-200">
                      {formatUsd(raw.w1Net)}
                    </td>
                    <td className="px-4 py-3 text-right text-slate-200">
                      {formatUsd(raw.w2Net)}
                    </td>
                    <td className="px-4 py-3 text-center text-slate-400">
                      {raw.chainCount > 0 ? formatNumber(raw.chainCount) : "-"}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </section>
    </main>
  );
}

