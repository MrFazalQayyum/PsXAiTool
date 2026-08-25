"use client";
import type { SectorPerf } from "@/lib/types";

interface Props { sectors: SectorPerf[]; }

function barWidth(pct: number): number {
  const max = 3;
  return Math.min(Math.abs(pct) / max, 1) * 100;
}

export function SectorGrid({ sectors }: Props) {
  if (sectors.length === 0) {
    return (
      <div className="bg-surface border border-border rounded-xl p-4">
        <span className="text-text3 text-sm">No sector data</span>
      </div>
    );
  }

  const allFlat = sectors.every(s => Math.abs(s.avg_change_pct) < 0.005);

  return (
    <div className="bg-surface border border-border rounded-xl p-4 h-full">
      <div className="mono text-2xs tracking-widest uppercase text-text3 mb-4 flex items-center gap-2">
        <span className="w-3 h-px bg-border-b inline-block" />
        Sector Performance
      </div>

      {allFlat ? (
        <div className="text-text3 text-xs py-2 mb-3">
          No significant sector movement — data may be from a non-trading day.
        </div>
      ) : null}

      <div className="flex flex-col gap-3.5 overflow-y-auto max-h-[420px] pr-1">
        {sectors.map((s) => {
          const isUp = s.avg_change_pct >= 0;
          const hasMove = Math.abs(s.avg_change_pct) >= 0.005;
          const color = !hasMove ? "text-text3" : isUp ? "text-accent" : "text-red";
          const barColor = !hasMove ? "bg-border-b" : isUp ? "bg-accent" : "bg-red";

          return (
            <div key={s.sector}>
              <div className="flex justify-between items-center mb-1.5">
                <span className="text-sm text-text1 font-medium truncate max-w-[160px]">{s.sector}</span>
                <div className="flex items-center gap-2 flex-shrink-0 ml-2">
                  <span className={`mono text-xs font-semibold tabular ${color}`}>
                    {s.avg_change_pct >= 0 ? "+" : ""}{s.avg_change_pct.toFixed(2)}%
                  </span>
                  <span className="mono text-2xs text-text3">{s.stock_count}co</span>
                </div>
              </div>
              <div className="h-1.5 bg-surface3 rounded-full overflow-hidden">
                <div
                  className={`h-full rounded-full transition-all duration-700 ${barColor} ${!hasMove ? "opacity-30" : ""}`}
                  style={{ width: `${Math.max(barWidth(s.avg_change_pct), hasMove ? 2 : 0)}%` }}
                />
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
