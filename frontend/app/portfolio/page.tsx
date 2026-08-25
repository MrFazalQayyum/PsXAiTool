"use client";
import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import type { Holding, PortfolioSummary } from "@/lib/types";

interface PortfolioResp {
  holdings: Holding[];
  summary: PortfolioSummary;
}

function fmt(n: number | null, prefix = "") {
  if (n === null || n === undefined) return "—";
  return `${prefix}${n.toLocaleString("en-PK", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function pnlColor(n: number | null) {
  if (n === null) return "text-text2";
  return n >= 0 ? "text-emerald-400" : "text-red-400";
}

function DirectionBadge({ dir }: { dir: string }) {
  const map: Record<string, string> = {
    bullish: "bg-emerald-500/15 text-emerald-400 border-emerald-500/30",
    bearish: "bg-red-500/15 text-red-400 border-red-500/30",
    neutral: "bg-zinc-500/15 text-zinc-400 border-zinc-500/30",
  };
  return (
    <span className={`inline-block px-1.5 py-0.5 rounded border mono text-2xs ${map[dir] ?? map.neutral}`}>
      {dir}
    </span>
  );
}

function AddHoldingForm({ onAdded }: { onAdded: () => void }) {
  const [symbol, setSymbol] = useState("");
  const [shares, setShares] = useState("");
  const [price, setPrice] = useState("");
  const [notes, setNotes] = useState("");
  const [err, setErr] = useState("");
  const [loading, setLoading] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!symbol || !shares || !price) { setErr("Fill all required fields"); return; }
    setLoading(true);
    setErr("");
    try {
      await api.post("/api/portfolio", {
        symbol: symbol.toUpperCase().trim(),
        shares_held: parseFloat(shares),
        avg_buy_price: parseFloat(price),
        notes,
      });
      setSymbol(""); setShares(""); setPrice(""); setNotes("");
      onAdded();
    } catch (e: any) {
      setErr(e.message || "Failed to add holding");
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={submit} className="bg-surface border border-border rounded-xl p-5 mb-6">
      <div className="mono text-2xs tracking-widest uppercase text-text3 mb-4">Add Holding</div>
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-3">
        <div>
          <label className="mono text-2xs text-text3 uppercase tracking-wide block mb-1">Symbol *</label>
          <input
            value={symbol}
            onChange={e => setSymbol(e.target.value.toUpperCase())}
            placeholder="OGDC"
            className="w-full bg-bg border border-border rounded-lg px-3 py-2 mono text-sm text-text1 focus:outline-none focus:border-accent"
          />
        </div>
        <div>
          <label className="mono text-2xs text-text3 uppercase tracking-wide block mb-1">Shares *</label>
          <input
            type="number"
            value={shares}
            onChange={e => setShares(e.target.value)}
            placeholder="500"
            className="w-full bg-bg border border-border rounded-lg px-3 py-2 mono text-sm text-text1 focus:outline-none focus:border-accent"
          />
        </div>
        <div>
          <label className="mono text-2xs text-text3 uppercase tracking-wide block mb-1">Avg Buy Price *</label>
          <input
            type="number"
            value={price}
            onChange={e => setPrice(e.target.value)}
            placeholder="250.00"
            className="w-full bg-bg border border-border rounded-lg px-3 py-2 mono text-sm text-text1 focus:outline-none focus:border-accent"
          />
        </div>
        <div>
          <label className="mono text-2xs text-text3 uppercase tracking-wide block mb-1">Notes</label>
          <input
            value={notes}
            onChange={e => setNotes(e.target.value)}
            placeholder="Optional"
            className="w-full bg-bg border border-border rounded-lg px-3 py-2 mono text-sm text-text1 focus:outline-none focus:border-accent"
          />
        </div>
      </div>
      {err && <p className="text-red-400 text-xs mono mb-2">{err}</p>}
      <button
        type="submit"
        disabled={loading}
        className="px-4 py-2 bg-accent/15 text-accent border border-accent/40 rounded-lg mono text-sm hover:bg-accent/25 transition-colors disabled:opacity-50"
      >
        {loading ? "Adding..." : "Add to Portfolio"}
      </button>
    </form>
  );
}

function HoldingCard({ holding, onDeleted }: { holding: Holding; onDeleted: () => void }) {
  const [deleting, setDeleting] = useState(false);

  async function del() {
    if (!confirm(`Remove ${holding.symbol} from portfolio?`)) return;
    setDeleting(true);
    try {
      await fetch(`/api/portfolio/${holding.symbol}`, { method: "DELETE" });
      onDeleted();
    } catch {
      setDeleting(false);
    }
  }

  return (
    <div className="bg-surface border border-border rounded-xl p-5">
      {/* Header */}
      <div className="flex items-start justify-between mb-4">
        <div>
          <div className="text-lg font-bold text-text1 mono">{holding.symbol}</div>
          <div className="text-xs text-text3 mono mt-0.5">
            {holding.shares_held.toLocaleString()} shares @ ₨{fmt(holding.avg_buy_price)}
          </div>
        </div>
        <button
          onClick={del}
          disabled={deleting}
          className="text-text3 hover:text-red-400 transition-colors mono text-xs"
        >
          remove
        </button>
      </div>

      {/* P&L Grid */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-4">
        <div>
          <div className="mono text-2xs text-text3 uppercase tracking-wide mb-1">Cost Basis</div>
          <div className="mono text-sm font-semibold text-text1">₨{fmt(holding.cost_basis)}</div>
        </div>
        <div>
          <div className="mono text-2xs text-text3 uppercase tracking-wide mb-1">Current Price</div>
          <div className="mono text-sm font-semibold text-text1">
            {holding.current_price != null ? `₨${fmt(holding.current_price)}` : "—"}
          </div>
        </div>
        <div>
          <div className="mono text-2xs text-text3 uppercase tracking-wide mb-1">Market Value</div>
          <div className="mono text-sm font-semibold text-text1">
            {holding.current_value != null ? `₨${fmt(holding.current_value)}` : "—"}
          </div>
        </div>
        <div>
          <div className="mono text-2xs text-text3 uppercase tracking-wide mb-1">P&L</div>
          <div className={`mono text-sm font-bold ${pnlColor(holding.pnl)}`}>
            {holding.pnl != null ? (
              <>
                {holding.pnl >= 0 ? "+" : ""}₨{fmt(holding.pnl)}
                <span className="text-xs font-normal ml-1">
                  ({holding.pnl >= 0 ? "+" : ""}{fmt(holding.pnl_pct)}%)
                </span>
              </>
            ) : "—"}
          </div>
        </div>
      </div>

      {/* Signals */}
      {holding.signals.length > 0 && (
        <div>
          <div className="mono text-2xs text-text3 uppercase tracking-wide mb-2">Recent Signals</div>
          <div className="flex flex-col gap-2">
            {holding.signals.map((s, i) => (
              <div key={i} className="flex items-start gap-2 text-xs text-text2">
                <DirectionBadge dir={s.direction} />
                <span className="text-text3 mono">
                  {(s.confidence * 100).toFixed(0)}%
                </span>
                <span className="flex-1">{s.summary}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {holding.notes && (
        <div className="mt-3 text-xs text-text3 mono border-t border-border pt-3">{holding.notes}</div>
      )}
    </div>
  );
}

function SummaryBar({ summary }: { summary: PortfolioSummary }) {
  return (
    <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-6">
      {[
        { label: "Holdings", value: summary.count.toString() },
        { label: "Total Cost", value: `₨${fmt(summary.total_cost)}` },
        { label: "Market Value", value: summary.total_value != null ? `₨${fmt(summary.total_value)}` : "—" },
        {
          label: "Total P&L",
          value: summary.total_pnl != null
            ? `${summary.total_pnl >= 0 ? "+" : ""}₨${fmt(summary.total_pnl)} (${summary.total_pnl >= 0 ? "+" : ""}${fmt(summary.total_pnl_pct)}%)`
            : "—",
          color: pnlColor(summary.total_pnl),
        },
      ].map(c => (
        <div key={c.label} className="bg-surface border border-border rounded-xl p-4">
          <div className="mono text-2xs tracking-widest uppercase text-text3 mb-2">{c.label}</div>
          <div className={`mono text-base font-bold tabular ${c.color ?? "text-text1"}`}>{c.value}</div>
        </div>
      ))}
    </div>
  );
}

export default function PortfolioPage() {
  const [data, setData] = useState<PortfolioResp | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await api.get<PortfolioResp>("/api/portfolio");
      setData(resp);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  return (
    <div className="min-h-screen bg-bg text-text1 p-6 max-w-5xl mx-auto">
      <div className="mb-8">
        <div className="mono text-2xs tracking-widest uppercase text-text3 mb-1">PSX Intelligence</div>
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-text1">Portfolio Tracker</h1>
          <a href="/" className="mono text-xs text-text3 hover:text-accent transition-colors">← Dashboard</a>
        </div>
      </div>

      <AddHoldingForm onAdded={load} />

      {loading ? (
        <div className="text-text3 mono text-sm">Loading portfolio...</div>
      ) : !data || data.holdings.length === 0 ? (
        <div className="bg-surface border border-border rounded-xl p-8 text-center">
          <div className="text-text3 mono text-sm">No holdings yet — add your first position above.</div>
        </div>
      ) : (
        <>
          <SummaryBar summary={data.summary} />
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {data.holdings.map(h => (
              <HoldingCard key={h.id} holding={h} onDeleted={load} />
            ))}
          </div>
        </>
      )}
    </div>
  );
}
