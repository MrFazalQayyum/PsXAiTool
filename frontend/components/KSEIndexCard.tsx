"use client";
import type { MarketIndex } from "@/lib/types";

function fmt(n: number | null, decimals = 2) {
  if (n === null || n === undefined) return "—";
  return n.toLocaleString("en-PK", { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
}

interface Props { index: MarketIndex; }

export function KSEIndexCard({ index }: Props) {
  const isUp = (index.change_pct ?? 0) >= 0;
  const hasChange = index.change_pct !== null && index.change_pct !== 0;

  return (
    <div className="bg-surface border border-border rounded-xl px-5 py-4 flex flex-col gap-2 hover:border-border-b transition-colors">
      <div className="flex items-center justify-between">
        <span className="mono text-2xs tracking-widest uppercase text-text3">{index.name}</span>
        {index.date && (
          <span className="mono text-2xs text-text3">
            {new Date(index.date).toLocaleDateString("en-PK", { day: "numeric", month: "short" })}
          </span>
        )}
      </div>
      <div className="tabular text-3xl font-bold text-text1 tracking-tight leading-none">
        {fmt(index.value, 0)}
      </div>
      {hasChange ? (
        <div className={`flex items-center gap-2 ${isUp ? "text-accent" : "text-red"}`}>
          <span className="mono text-sm font-semibold tabular">
            {isUp ? "▲" : "▼"} {fmt(Math.abs(index.change ?? 0), 2)}
          </span>
          <span className={`mono text-xs px-2 py-0.5 rounded-full font-medium tabular ${
            isUp ? "bg-accent/10" : "bg-red/10"
          }`}>
            {isUp ? "+" : ""}{fmt(index.change_pct, 2)}%
          </span>
        </div>
      ) : (
        <div className="text-text3 text-sm">No change data</div>
      )}
    </div>
  );
}
