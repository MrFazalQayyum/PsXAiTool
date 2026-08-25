"use client";
import type { MarketSignal } from "@/lib/types";

const DIRECTION_COLOR: Record<string, string> = {
  bullish: "text-accent",
  bearish: "text-red",
  neutral: "text-text2",
};

const DIRECTION_BG: Record<string, string> = {
  bullish: "bg-accent/10 border-accent/30",
  bearish: "bg-red/10 border-red/30",
  neutral: "bg-surface3 border-border",
};

const DIRECTION_ICON: Record<string, string> = {
  bullish: "▲",
  bearish: "▼",
  neutral: "→",
};

function ConfidenceBar({ value }: { value: number }) {
  const pct = Math.round(value * 100);
  const color = pct >= 80 ? "bg-accent" : pct >= 60 ? "bg-amber" : "bg-text3";
  return (
    <div className="flex items-center gap-2">
      <div className="flex-1 h-1 bg-surface3 rounded-full overflow-hidden">
        <div className={`h-full rounded-full ${color}`} style={{ width: `${pct}%` }} />
      </div>
      <span className={`mono text-2xs tabular ${pct >= 80 ? "text-accent" : pct >= 60 ? "text-amber" : "text-text3"}`}>
        {pct}%
      </span>
    </div>
  );
}

interface Props {
  signal: MarketSignal;
  conflictedEntities?: Set<string>;
}

export function SignalCard({ signal, conflictedEntities }: Props) {
  const dir = signal.direction ?? "neutral";
  const label = (signal.signal_type ?? "unknown").replace(/_/g, " ");
  const timeStr = signal.created_at
    ? new Date(signal.created_at).toLocaleString("en-PK", {
        month: "short", day: "numeric", hour: "2-digit", minute: "2-digit",
      })
    : "";

  const hasConflict = conflictedEntities && signal.entities.some(e => conflictedEntities.has(e));

  return (
    <div className={`border rounded-xl p-4 ${DIRECTION_BG[dir]}`}>
      {/* Header row */}
      <div className="flex items-start justify-between gap-3 mb-3">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <span className={`mono text-xs font-bold uppercase tracking-wide ${DIRECTION_COLOR[dir]}`}>
              {DIRECTION_ICON[dir]} {dir}
            </span>
            {signal.is_notified && (
              <span className="mono text-2xs text-text3 bg-surface3 px-1.5 py-0.5 rounded">notified</span>
            )}
            {hasConflict && (
              <span className="mono text-2xs text-amber bg-amber/10 border border-amber/30 px-1.5 py-0.5 rounded">
                ⚡ mixed signals
              </span>
            )}
          </div>
          <p className="text-text1 text-sm font-semibold capitalize leading-tight">{label}</p>
        </div>
        <span className="mono text-2xs text-text3 flex-shrink-0 mt-0.5">{timeStr}</span>
      </div>

      {/* Summary */}
      {signal.summary && (
        <p className="text-text2 text-sm leading-relaxed mb-3">{signal.summary}</p>
      )}

      {/* Tickers */}
      {signal.entities.length > 0 && (
        <div className="flex flex-wrap gap-1.5 mb-3">
          {signal.entities.map((t) => {
            const isConflicted = conflictedEntities?.has(t);
            return (
              <span
                key={t}
                className={`mono text-2xs font-semibold px-2 py-0.5 rounded border ${
                  isConflicted
                    ? "bg-amber/10 text-amber border-amber/40"
                    : "bg-surface3 text-text1 border-border"
                }`}
              >
                {t}{isConflicted ? " ⚡" : ""}
              </span>
            );
          })}
          {signal.sectors.slice(0, 2).map((s) => (
            <span key={s} className="text-2xs px-2 py-0.5 text-text3 rounded border border-border/50">
              {s}
            </span>
          ))}
        </div>
      )}

      {/* Conflict note */}
      {hasConflict && (
        <p className="text-amber/80 text-xs mono mb-3 leading-relaxed">
          ⚡ This stock also has a conflicting signal in today&apos;s feed — this signal is based on a specific event,
          not the stock&apos;s overall direction.
        </p>
      )}

      {/* Confidence */}
      <div className="mb-3">
        <div className="mono text-2xs text-text3 mb-1 uppercase tracking-wide">Confidence</div>
        <ConfidenceBar value={signal.confidence} />
      </div>

      {/* Historical note */}
      {signal.historical_note && (
        <p className="text-text3 text-xs italic leading-relaxed border-t border-border/50 pt-2 mt-2">
          📖 {signal.historical_note}
        </p>
      )}

      {/* Source headline */}
      {signal.raw_headline && signal.raw_headline !== signal.summary && (
        <div className="mt-2 border-t border-border/30 pt-2">
          {signal.source_url ? (
            <a
              href={signal.source_url}
              target="_blank"
              rel="noopener noreferrer"
              className="text-text3 text-xs hover:text-text2 transition-colors truncate block"
            >
              ↗ {signal.raw_headline}
            </a>
          ) : (
            <p className="text-text3 text-xs truncate">{signal.raw_headline}</p>
          )}
          {signal.source && (
            <span className="mono text-2xs text-text3 uppercase tracking-wide">{signal.source}</span>
          )}
        </div>
      )}
    </div>
  );
}
