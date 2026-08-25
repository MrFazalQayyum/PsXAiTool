"use client";
import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import type { MarketSignal, SignalStats } from "@/lib/types";
import { SignalCard } from "@/components/SignalCard";

const FILTER_DIRS = ["all", "bullish", "bearish", "neutral"] as const;
type FilterDir = typeof FILTER_DIRS[number];

function StatPill({ label, value, accent }: { label: string; value: string | number | null; accent?: string }) {
  return (
    <div className="bg-surface border border-border rounded-lg px-4 py-3 text-center">
      <div className={`text-xl font-bold mono tabular ${accent ?? "text-text1"}`}>
        {value ?? "—"}
      </div>
      <div className="mono text-2xs text-text3 uppercase tracking-wide mt-0.5">{label}</div>
    </div>
  );
}

function ConflictBanner({ entities }: { entities: string[] }) {
  if (entities.length === 0) return null;
  return (
    <div className="mb-5 p-4 rounded-xl border border-amber/40 bg-amber/5">
      <div className="flex items-start gap-3">
        <span className="text-amber text-lg leading-none mt-0.5">⚡</span>
        <div className="flex-1">
          <div className="mono text-xs font-semibold text-amber uppercase tracking-wide mb-1">
            Mixed Signals — Read Carefully
          </div>
          <p className="text-text2 text-xs leading-relaxed mb-2">
            The following stocks have <strong>both bullish and bearish signals</strong> in the last 24 hours from
            different news sources. Each signal reflects a different event — they are not errors.
            Review both sides before trading.
          </p>
          <div className="flex flex-wrap gap-1.5">
            {entities.map((e) => (
              <span
                key={e}
                className="mono text-xs px-2 py-0.5 rounded border border-amber/50 bg-amber/10 text-amber font-semibold"
              >
                {e}
              </span>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

export default function SignalsPage() {
  const [signals, setSignals] = useState<MarketSignal[]>([]);
  const [stats, setStats] = useState<SignalStats | null>(null);
  const [conflicted, setConflicted] = useState<string[]>([]);
  const [filter, setFilter] = useState<FilterDir>("all");
  const [entityFilter, setEntityFilter] = useState("");
  const [minConf, setMinConf] = useState(0);
  const [loading, setLoading] = useState(true);
  const [triggering, setTriggering] = useState(false);
  const [message, setMessage] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [sigData, statsData, conflictData] = await Promise.all([
        api.signals(150, minConf),
        api.signalStats(),
        api.get<{ conflicted_entities: string[] }>("/api/signals/conflicts"),
      ]);

      let sigs = sigData.signals;
      if (filter !== "all") sigs = sigs.filter((s) => s.direction === filter);
      if (entityFilter.trim()) {
        const q = entityFilter.trim().toUpperCase();
        sigs = sigs.filter((s) => s.entities.some((e) => e.toUpperCase().includes(q)));
      }

      setSignals(sigs);
      setStats(statsData);
      setConflicted(conflictData.conflicted_entities || []);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  }, [filter, minConf, entityFilter]);

  useEffect(() => { load(); }, [load]);

  async function triggerFetch() {
    setTriggering(true);
    setMessage("");
    try {
      const r = await api.triggerNewsFetch();
      setMessage(r.message ?? "Queued");
      setTimeout(load, 8000);
    } catch {
      setMessage("Failed to queue — is the Celery worker running?");
    } finally {
      setTriggering(false);
    }
  }

  // Compute which entities in current view have mixed signals
  const viewBullish = new Set(signals.filter(s => s.direction === "bullish").flatMap(s => s.entities));
  const viewBearish = new Set(signals.filter(s => s.direction === "bearish").flatMap(s => s.entities));
  const visibleConflicts = new Set([...viewBullish].filter(e => viewBearish.has(e)));

  return (
    <div className="min-h-screen bg-bg text-text1 p-4 sm:p-6">
      <div className="max-w-5xl mx-auto">

        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-xl font-bold text-text1">Market Signals</h1>
            <p className="text-text3 text-sm mt-0.5">AI-extracted PSX intelligence from news</p>
          </div>
          <div className="flex items-center gap-3">
            <a href="/" className="mono text-xs text-text3 hover:text-text2 transition-colors">← Dashboard</a>
            <a href="/portfolio" className="mono text-xs text-text3 hover:text-accent transition-colors">Portfolio</a>
            <button
              onClick={triggerFetch}
              disabled={triggering}
              className="mono text-xs px-3 py-1.5 rounded-lg border border-border-b text-text2
                         hover:text-text1 hover:border-accent/60 transition-colors disabled:opacity-50"
            >
              {triggering ? "Queuing…" : "Fetch News"}
            </button>
          </div>
        </div>

        {message && (
          <div className="mb-4 px-4 py-2 rounded-lg bg-surface border border-border text-text2 text-sm mono">
            {message}
          </div>
        )}

        {/* Stats row */}
        {stats && (
          <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-3 mb-6">
            <StatPill label="Signals" value={stats.total_signals} />
            <StatPill label="Bullish" value={stats.bullish} accent="text-accent" />
            <StatPill label="Bearish" value={stats.bearish} accent="text-red" />
            <StatPill label="Articles" value={stats.total_articles} />
            <StatPill
              label="Filter Rate"
              value={stats.filter_rate_pct !== null ? `${stats.filter_rate_pct}%` : null}
            />
            <StatPill
              label="Accuracy"
              value={stats.accuracy_pct !== null ? `${stats.accuracy_pct}%` : "—"}
              accent={stats.accuracy_pct !== null
                ? (stats.accuracy_pct >= 60 ? "text-accent" : "text-amber")
                : undefined}
            />
          </div>
        )}

        {/* Conflict banner — only when visible in current view */}
        {visibleConflicts.size > 0 && (
          <ConflictBanner entities={[...visibleConflicts].sort()} />
        )}

        {/* Filters */}
        <div className="flex flex-wrap items-center gap-3 mb-5">
          <div className="flex gap-1">
            {FILTER_DIRS.map((d) => (
              <button
                key={d}
                onClick={() => setFilter(d)}
                className={`mono text-xs px-3 py-1.5 rounded-lg capitalize transition-colors border ${
                  filter === d
                    ? d === "bullish"
                      ? "bg-accent/15 text-accent border-accent/40"
                      : d === "bearish"
                        ? "bg-red/10 text-red border-red/40"
                        : "bg-surface3 text-text1 border-border-b"
                    : "text-text3 border-border hover:border-border-b hover:text-text2"
                }`}
              >
                {d}
              </button>
            ))}
          </div>

          {/* Entity / ticker filter */}
          <input
            value={entityFilter}
            onChange={(e) => setEntityFilter(e.target.value.toUpperCase())}
            placeholder="Filter by ticker (e.g. OGDC)"
            className="mono text-xs px-3 py-1.5 rounded-lg border border-border bg-surface text-text2
                       focus:outline-none focus:border-accent w-44 placeholder-text3"
          />

          <div className="flex items-center gap-2 ml-auto">
            <span className="mono text-2xs text-text3 uppercase">Min confidence</span>
            <select
              value={minConf}
              onChange={(e) => setMinConf(Number(e.target.value))}
              className="bg-surface2 border border-border text-text2 text-xs rounded-lg px-2 py-1.5
                         mono focus:outline-none focus:border-accent"
            >
              <option value={0}>Any</option>
              <option value={0.5}>50%+</option>
              <option value={0.7}>70%+</option>
              <option value={0.85}>85%+</option>
            </select>
          </div>
        </div>

        {/* Signal grid */}
        {loading ? (
          <div className="text-center py-20 text-text3 text-sm mono">Loading signals…</div>
        ) : signals.length === 0 ? (
          <div className="text-center py-20">
            <p className="text-text3 text-sm">No signals match the current filters.</p>
            <p className="text-text3 text-xs mt-2">
              Click <span className="text-text2">Fetch News</span> to run the scraper and generate signals.
            </p>
          </div>
        ) : (
          <>
            <p className="mono text-2xs text-text3 mb-3">
              {signals.length} signals shown
              {visibleConflicts.size > 0 && (
                <span className="text-amber ml-2">
                  ⚡ {visibleConflicts.size} stock{visibleConflicts.size > 1 ? "s" : ""} with mixed signals
                </span>
              )}
            </p>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {signals.map((s) => (
                <SignalCard
                  key={s.id}
                  signal={s}
                  conflictedEntities={visibleConflicts}
                />
              ))}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
