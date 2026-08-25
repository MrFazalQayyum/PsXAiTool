"use client";
import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import type { MarketIndex, StockMover, SectorPerf, StockSummary } from "@/lib/types";
import { KSEIndexCard } from "@/components/KSEIndexCard";
import { TopMovers } from "@/components/TopMovers";
import { SectorGrid } from "@/components/SectorGrid";
import { StockChart } from "@/components/StockChart";

const REFRESH_MS = 5 * 60 * 1000;

function PktClock() {
  const [time, setTime] = useState("");
  useEffect(() => {
    const tick = () => {
      setTime(
        new Date().toLocaleTimeString("en-PK", {
          timeZone: "Asia/Karachi",
          hour: "2-digit",
          minute: "2-digit",
          second: "2-digit",
          hour12: false,
        }) + " PKT"
      );
    };
    tick();
    const id = setInterval(tick, 1000);
    return () => clearInterval(id);
  }, []);
  return <span className="mono text-xs text-text2 tabular">{time}</span>;
}

export default function Dashboard() {
  const [indices, setIndices] = useState<MarketIndex[]>([]);
  const [gainers, setGainers] = useState<StockMover[]>([]);
  const [losers, setLosers] = useState<StockMover[]>([]);
  const [sectors, setSectors] = useState<SectorPerf[]>([]);
  const [stocks, setStocks] = useState<StockSummary[]>([]);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const [fetching, setFetching] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadAll = useCallback(async () => {
    setError(null);
    try {
      const [idxRes, moversRes, secRes, stocksRes] = await Promise.all([
        api.indices(),
        api.topMovers(5),
        api.sectors(),
        api.stocks(),
      ]);
      setIndices(idxRes.indices);
      setGainers(moversRes.gainers);
      setLosers(moversRes.losers);
      setSectors(secRes.sectors);
      setStocks(stocksRes.stocks);
      setLastUpdated(new Date());
    } catch {
      setError("Could not reach the backend. Make sure it's running on port 8000.");
    }
  }, []);

  useEffect(() => {
    loadAll();
    const id = setInterval(loadAll, REFRESH_MS);
    return () => clearInterval(id);
  }, [loadAll]);

  async function handleFetchNow() {
    setFetching(true);
    try {
      await api.triggerFetch();
      setTimeout(loadAll, 3000);
    } finally {
      setFetching(false);
    }
  }

  const marketOpen = (() => {
    const now = new Date();
    const pkt = new Date(now.toLocaleString("en-US", { timeZone: "Asia/Karachi" }));
    const day = pkt.getDay();
    const h = pkt.getHours(), m = pkt.getMinutes();
    const mins = h * 60 + m;
    return day >= 1 && day <= 5 && mins >= 555 && mins <= 930; // 9:15–15:30
  })();

  return (
    <div className="min-h-screen bg-bg">
      {/* Header */}
      <header className="border-b border-border px-6 py-3.5 flex items-center justify-between sticky top-0 bg-bg/95 backdrop-blur z-10">
        <div className="flex items-center gap-3">
          <span className={`w-2 h-2 rounded-full ${marketOpen ? "bg-accent animate-pulse" : "bg-text3"}`} />
          <span className="mono text-sm font-bold tracking-widest uppercase text-text1">
            PSX Intelligence
          </span>
          <span className={`text-2xs px-2 py-0.5 rounded-full border mono tracking-widest uppercase ${
            marketOpen
              ? "border-accent/30 text-accent bg-accent/5"
              : "border-border text-text3"
          }`}>
            {marketOpen ? "Live" : "Closed"}
          </span>
        </div>
        <div className="flex items-center gap-3 sm:gap-5">
          <PktClock />
          {lastUpdated && (
            <span className="mono text-2xs text-text3 hidden md:block">
              updated {lastUpdated.toLocaleTimeString("en-PK", { timeZone: "Asia/Karachi", hour: "2-digit", minute: "2-digit" })}
            </span>
          )}
          <a
            href="/signals"
            className="mono text-2xs tracking-widest uppercase px-3.5 py-1.5 rounded border border-border text-text2
                       hover:border-amber hover:text-amber transition-all duration-200"
          >
            Signals
          </a>
          <a
            href="/portfolio"
            className="mono text-2xs tracking-widest uppercase px-3.5 py-1.5 rounded border border-border text-text2
                       hover:border-accent hover:text-accent transition-all duration-200"
          >
            Portfolio
          </a>
          <button
            onClick={handleFetchNow}
            disabled={fetching}
            className="mono text-2xs tracking-widest uppercase px-3.5 py-1.5 rounded border border-border text-text2
                       hover:border-accent hover:text-accent transition-all duration-200 disabled:opacity-40"
          >
            {fetching ? "Fetching…" : "Fetch Now"}
          </button>
        </div>
      </header>

      <main className="max-w-[1280px] mx-auto px-5 py-6 flex flex-col gap-6">

        {error && (
          <div className="bg-red/8 border border-red/30 rounded-lg px-5 py-3.5 text-sm text-red/90 flex items-center gap-3">
            <span className="text-red font-bold">!</span>
            {error}
          </div>
        )}

        {/* Indices row */}
        <section>
          <SectionLabel>Market Indices</SectionLabel>
          {indices.length === 0 ? (
            <EmptyState>No index data — use Fetch Now or wait for the daily task.</EmptyState>
          ) : (
            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
              {indices.map((idx) => <KSEIndexCard key={idx.name} index={idx} />)}
            </div>
          )}
        </section>

        {/* Top movers */}
        <section>
          <SectionLabel>Today's Movers</SectionLabel>
          <TopMovers gainers={gainers} losers={losers} />
        </section>

        {/* Sector + Chart */}
        <section className="grid grid-cols-1 lg:grid-cols-5 gap-4">
          <div className="lg:col-span-2">
            <SectorGrid sectors={sectors} />
          </div>
          <div className="lg:col-span-3">
            {stocks.length > 0 ? (
              <StockChart stocks={stocks} />
            ) : (
              <EmptyState>Load stock data to view chart</EmptyState>
            )}
          </div>
        </section>

        {/* Stocks table */}
        <section>
          <SectionLabel>All Stocks — {stocks.filter(s => s.close !== null).length} with prices</SectionLabel>
          <div className="bg-surface border border-border rounded-xl overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border">
                  {["Symbol", "Company", "Sector", "Close (PKR)", "Change", "Volume"].map((h, i) => (
                    <th key={h} className={`mono text-2xs tracking-widest uppercase text-text3 px-4 py-3 font-medium ${i >= 4 ? "text-right" : i === 3 ? "text-right" : "text-left"} ${i === 5 ? "hidden lg:table-cell" : i === 2 ? "hidden md:table-cell" : i === 1 ? "hidden sm:table-cell" : ""}`}>
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border/60">
                {stocks.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="px-4 py-8 text-center text-text3 text-sm">
                      No stock data yet — seed companies and run a price fetch first.
                    </td>
                  </tr>
                ) : (
                  stocks.map((s) => {
                    const isUp = (s.change_pct ?? 0) >= 0;
                    const hasChange = s.change_pct !== null && s.change_pct !== 0;
                    return (
                      <tr key={s.symbol} className="hover:bg-surface2/60 transition-colors">
                        <td className="px-4 py-3">
                          <span className="mono font-semibold text-text1 text-sm">{s.symbol}</span>
                        </td>
                        <td className="px-4 py-3 hidden sm:table-cell">
                          <span className="text-text2 text-sm truncate max-w-[200px] block">{s.name}</span>
                        </td>
                        <td className="px-4 py-3 hidden md:table-cell">
                          <span className="text-xs text-text3 bg-surface2 px-2 py-0.5 rounded-full">{s.sector}</span>
                        </td>
                        <td className="px-4 py-3 text-right">
                          <span className="mono font-semibold text-text1 tabular">
                            {s.close !== null ? s.close.toLocaleString("en-PK", { minimumFractionDigits: 2 }) : "—"}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right">
                          {hasChange ? (
                            <span className={`mono text-xs font-semibold px-2 py-0.5 rounded-full tabular ${
                              isUp ? "bg-accent/10 text-accent" : "bg-red/10 text-red"
                            }`}>
                              {isUp ? "▲" : "▼"} {Math.abs(s.change_pct!).toFixed(2)}%
                            </span>
                          ) : (
                            <span className="text-text3 text-xs">—</span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-right text-text3 mono text-xs tabular hidden lg:table-cell">
                          {s.volume ? s.volume.toLocaleString() : "—"}
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </section>
      </main>
    </div>
  );
}

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <div className="mono text-2xs tracking-widest uppercase text-text3 mb-3 flex items-center gap-2">
      <span className="w-3 h-px bg-border-b inline-block" />
      {children}
    </div>
  );
}

function EmptyState({ children }: { children: React.ReactNode }) {
  return (
    <div className="bg-surface border border-border rounded-xl px-5 py-5 text-text3 text-sm">
      {children}
    </div>
  );
}
