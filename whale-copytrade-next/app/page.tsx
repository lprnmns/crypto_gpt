export default function HomePage() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-8 bg-slate-950 p-8 text-slate-100">
      <section className="max-w-xl text-center">
        <h1 className="text-3xl font-semibold">Whale Copytrade Dashboard</h1>
        <p className="mt-4 text-base text-slate-300">
          10 Ekim 2025 crash penceresindeki balina cüzdanlarını analiz eden bu
          Next.js uygulamasında TypeScript, Prisma ve viem katmanlarıyla tam bir
          veri pipeline&apos;ı kuruyoruz. Aday cüzdanlar ve skorları anlık
          olarak görüntüleyebilirsin.
        </p>
        <div className="mt-6 flex justify-center">
          <a
            href="/candidates"
            className="rounded-md bg-sky-500 px-4 py-2 text-sm font-medium text-white shadow hover:bg-sky-400"
          >
            Skor Tablosunu Gör
          </a>
        </div>
      </section>
    </main>
  );
}

