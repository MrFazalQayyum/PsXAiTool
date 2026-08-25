import type {
  MarketIndex,
  StockMover,
  SectorPerf,
  StockSummary,
  StockDetail,
  MarketSignal,
  SignalStats,
} from "./types";

const BASE = "/api/market";
const SIGNALS_BASE = "/api/signals";

async function get<T>(path: string): Promise<T> {
  const url = path.startsWith("/api/") ? path : BASE + path;
  const res = await fetch(url, { cache: "no-store" });
  if (!res.ok) throw new Error(`API error ${res.status}: ${path}`);
  return res.json() as Promise<T>;
}

async function post<T>(path: string, body: unknown = {}): Promise<T> {
  const res = await fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`API error ${res.status}: ${path}`);
  return res.json() as Promise<T>;
}

export const api = {
  indices: () =>
    get<{ indices: MarketIndex[] }>("/indices"),

  topMovers: (limit = 5) =>
    get<{ gainers: StockMover[]; losers: StockMover[]; date: string | null }>(
      `/top-movers?limit=${limit}`
    ),

  sectors: () =>
    get<{ sectors: SectorPerf[]; date: string | null }>("/sectors"),

  stocks: (sector?: string) =>
    get<{ stocks: StockSummary[] }>(
      "/stocks" + (sector ? `?sector=${encodeURIComponent(sector)}` : "")
    ),

  stockPrices: (symbol: string, days = 30) =>
    get<StockDetail>(`/stocks/${symbol}/prices?days=${days}`),

  triggerFetch: () =>
    fetch(BASE + "/admin/fetch-prices", { method: "POST" }).then((r) => r.json()),

  signals: (limit = 50, minConfidence = 0) =>
    fetch(`${SIGNALS_BASE}?limit=${limit}&min_confidence=${minConfidence}`, { cache: "no-store" })
      .then((r) => r.json() as Promise<{ signals: MarketSignal[]; count: number }>),

  signalStats: () =>
    fetch(`${SIGNALS_BASE}/stats`, { cache: "no-store" })
      .then((r) => r.json() as Promise<SignalStats>),

  triggerNewsFetch: () =>
    fetch(`${SIGNALS_BASE}/trigger`, { method: "POST" }).then((r) => r.json()),

  get: <T>(path: string) => get<T>(path),
  post: <T>(path: string, body: unknown = {}) => post<T>(path, body),
};
