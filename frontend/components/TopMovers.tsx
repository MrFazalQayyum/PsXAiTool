"use client";
import type { StockMover } from "@/lib/types";

interface Props {
  gainers: StockMover[];
  losers: StockMover[];
}

function MoverRow({ stock, type }: { stock: StockMover; type: "gainer" | "loser" }) {
  const isUp = type === "gainer";
  const pct = stock.change_pct ?? 0;
  const hasChange = pct !== 0;

  return (
    <div className="flex items-center justify-between py-2.5 border-b border-border/60 last:border-0">
      <div className="flex flex-col min-w-0 gap-0.5">
        <span className="mono font-semibold text-text1 text-sm">{stock.symbol}</span>
        <span className="text-xs text-text3 truncate max-w-[160px]">{stock.name}</span>
      </div>
      <div className="flex flex-col items-end gap-0.5 ml-3 flex-shrink-0">
        <span className="mono font-semibold text-text1 tabular text-sm">
          {stock.close !== null ? stock.close.toLocaleString("en-PK", { minimumFractionDigits: 2 }) : "—"}
        </span>
        {hasChange ? (
          <span className={`mono text-xs font-semibold px-2 py-0.5 rounded-full tabular ${
            isUp ? "bg-accent/10 text-accent" : "bg-red/10 text-red"
          }`}>
            {isUp ? "▲" : "▼"} {Math.abs(pct).toFixed(2)}%
          </span>
        ) : (
          <span className="mono text-xs text-text3 px-2">0.00%</span>
        )}
      </div>
    </div>
  );
}

export function TopMovers({ gainers, losers }: Props) {
  const hasSignificantMovers = gainers.some(s => Math.abs(s.change_pct ?? 0) > 0.01) ||
                               losers.some(s => Math.abs(s.change_pct ?? 0) > 0.01);

  if (!gainers.length && !losers.length) {
    return (
      <div className="bg-surface border border-border rounded-xl px-5 py-5 text-text3 text-sm">
        No mover data — run a price fetch first.
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
      {/* Gainers */}
      <div className="bg-surface border border-border rounded-xl p-4">
        <div className="flex items-center gap-2 mb-3">
          <span className="w-1.5 h-4 rounded-full bg-accent inline-block" />
          <span className="mono text-2xs tracking-widest uppercase text-accent font-medium">Top Gainers</span>
        </div>
        {!hasSignificantMovers ? (
          <p className="text-text3 text-xs py-2">No significant movement today</p>
        ) : gainers.length === 0 ? (
          <p className="text-text3 text-xs py-2">No data</p>
        ) : (
          gainers.map(s => <MoverRow key={s.symbol} stock={s} type="gainer" />)
        )}
      </div>

      {/* Losers */}
      <div className="bg-surface border border-border rounded-xl p-4">
        <div className="flex items-center gap-2 mb-3">
          <span className="w-1.5 h-4 rounded-full bg-red inline-block" />
          <span className="mono text-2xs tracking-widest uppercase text-red font-medium">Top Losers</span>
        </div>
        {!hasSignificantMovers ? (
          <p className="text-text3 text-xs py-2">No significant movement today</p>
        ) : losers.length === 0 ? (
          <p className="text-text3 text-xs py-2">No data</p>
        ) : (
          losers.map(s => <MoverRow key={s.symbol} stock={s} type="loser" />)
        )}
      </div>
    </div>
  );
}
