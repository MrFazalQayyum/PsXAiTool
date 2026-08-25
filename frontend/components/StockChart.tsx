"use client";
import { useEffect, useState, useCallback } from "react";
import {
  AreaChart, Area, XAxis, YAxis, Tooltip,
  ResponsiveContainer, CartesianGrid,
} from "recharts";
import { api } from "@/lib/api";
import type { PricePoint, StockSummary } from "@/lib/types";

const PERIODS = [
  { label: "1M", days: 30 },
  { label: "3M", days: 90 },
  { label: "6M", days: 180 },
  { label: "1Y", days: 365 },
];

interface Props { stocks: StockSummary[]; }

function CustomTooltip({ active, payload, label }: any) {
  if (!active || !payload?.length) return null;
  const d = payload[0].payload as PricePoint;
  const isUp = (d.change_pct ?? 0) >= 0;
  return (
    <div className="bg-surface2 border border-border-b rounded-lg px-4 py-3 shadow-xl text-sm">
      <div className="text-text3 mono text-xs mb-2">{label}</div>
      <div className="text-text1 font-bold text-base tabular">
        PKR {d.close?.toLocaleString("en-PK", { minimumFractionDigits: 2 })}
      </div>
      {d.change_pct !== null && d.change_pct !== 0 && (
        <div className={`mono text-xs font-semibold mt-1 ${isUp ? "text-accent" : "text-red"}`}>
          {isUp ? "▲" : "▼"} {Math.abs(d.change_pct).toFixed(2)}%
        </div>
      )}
      {d.volume != null && d.volume > 0 && (
        <div className="text-text3 text-xs mt-1.5 mono">Vol: {d.volume.toLocaleString()}</div>
      )}
    </div>
  );
}

export function StockChart({ stocks }: Props) {
  const [selectedSymbol, setSelectedSymbol] = useState<string>(stocks[0]?.symbol ?? "");
  const [days, setDays] = useState(30);
  const [prices, setPrices] = useState<PricePoint[]>([]);
  const [stockName, setStockName] = useState("");
  const [loading, setLoading] = useState(false);

  const load = useCallback(async () => {
    if (!selectedSymbol) return;
    setLoading(true);
    try {
      const data = await api.stockPrices(selectedSymbol, days);
      setPrices(data.prices);
      setStockName(data.name);
    } catch {
      setPrices([]);
    } finally {
      setLoading(false);
    }
  }, [selectedSymbol, days]);

  useEffect(() => { load(); }, [load]);

  const first = prices[0]?.close ?? null;
  const last = prices[prices.length - 1]?.close ?? null;
  const overallUp = first !== null && last !== null && last >= first;
  const strokeColor = overallUp ? "#00C49A" : "#E0536A";

  const validPrices = prices.filter(p => p.close != null);
  const yMin = validPrices.length
    ? Math.floor(Math.min(...validPrices.map(p => p.low ?? p.close ?? 0)) * 0.995)
    : "auto";
  const yMax = validPrices.length
    ? Math.ceil(Math.max(...validPrices.map(p => p.high ?? p.close ?? 0)) * 1.005)
    : "auto";

  const latestPrice = prices[prices.length - 1];
  const pctChange = (() => {
    if (!first || !last || first === last) return null;
    return ((last - first) / first * 100).toFixed(2);
  })();

  return (
    <div className="bg-surface border border-border rounded-xl p-5 h-full flex flex-col">
      {/* Controls */}
      <div className="flex flex-wrap items-center gap-3 mb-4">
        <select
          value={selectedSymbol}
          onChange={e => setSelectedSymbol(e.target.value)}
          className="bg-surface2 border border-border text-text1 text-sm rounded-lg px-3 py-1.5
                     mono focus:outline-none focus:border-accent flex-1 min-w-0 max-w-xs"
        >
          {stocks.map(s => (
            <option key={s.symbol} value={s.symbol}>{s.symbol} — {s.name}</option>
          ))}
        </select>
        <div className="flex gap-1 ml-auto">
          {PERIODS.map(p => (
            <button
              key={p.label}
              onClick={() => setDays(p.days)}
              className={`mono text-xs px-3 py-1.5 rounded-lg transition-colors ${
                days === p.days
                  ? "bg-accent/15 text-accent border border-accent/40"
                  : "text-text2 border border-border hover:border-border-b hover:text-text1"
              }`}
            >
              {p.label}
            </button>
          ))}
        </div>
      </div>

      {/* Stock info row */}
      {stockName && (
        <div className="flex items-baseline gap-3 mb-4">
          <span className="mono font-bold text-text1">{selectedSymbol}</span>
          <span className="text-text2 text-sm">{stockName}</span>
          {latestPrice?.close && (
            <span className="ml-auto mono font-bold text-text1 tabular text-lg">
              {latestPrice.close.toLocaleString("en-PK", { minimumFractionDigits: 2 })}
            </span>
          )}
          {pctChange !== null && (
            <span className={`mono text-sm font-semibold tabular ${overallUp ? "text-accent" : "text-red"}`}>
              {overallUp ? "▲" : "▼"} {Math.abs(Number(pctChange)).toFixed(2)}%
            </span>
          )}
        </div>
      )}

      {/* Chart */}
      {loading ? (
        <div className="flex-1 flex items-center justify-center text-text3 text-sm">Loading…</div>
      ) : prices.length === 0 ? (
        <div className="flex-1 flex items-center justify-center text-text3 text-sm">No price data</div>
      ) : (
        <div className="flex-1 min-h-[200px]">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={prices} margin={{ top: 4, right: 4, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="priceGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor={strokeColor} stopOpacity={0.18} />
                  <stop offset="95%" stopColor={strokeColor} stopOpacity={0.01} />
                </linearGradient>
              </defs>
              <CartesianGrid stroke="#1C334F" strokeDasharray="2 4" vertical={false} />
              <XAxis
                dataKey="date"
                tick={{ fill: "#3A5570", fontSize: 11, fontFamily: "JetBrains Mono, monospace" }}
                tickLine={false}
                axisLine={false}
                interval="preserveStartEnd"
                tickFormatter={(d: string) => {
                  const dt = new Date(d);
                  return dt.toLocaleDateString("en-PK", { day: "numeric", month: "short" });
                }}
              />
              <YAxis
                domain={[yMin, yMax]}
                tick={{ fill: "#3A5570", fontSize: 11, fontFamily: "JetBrains Mono, monospace" }}
                tickLine={false}
                axisLine={false}
                width={70}
                tickFormatter={(v: number) => v.toLocaleString("en-PK", { maximumFractionDigits: 0 })}
              />
              <Tooltip content={<CustomTooltip />} />
              <Area
                type="monotone"
                dataKey="close"
                stroke={strokeColor}
                strokeWidth={2}
                fill="url(#priceGrad)"
                dot={false}
                activeDot={{ r: 5, fill: strokeColor, strokeWidth: 0 }}
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}
