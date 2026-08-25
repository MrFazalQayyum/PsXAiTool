export interface MarketIndex {
  name: string;
  value: number | null;
  change: number | null;
  change_pct: number | null;
  volume: number | null;
  date: string | null;
}

export interface StockMover {
  symbol: string;
  name: string;
  sector: string;
  close: number | null;
  change_pct: number | null;
  volume: number | null;
}

export interface SectorPerf {
  sector: string;
  avg_change_pct: number;
  stock_count: number;
}

export interface StockSummary {
  symbol: string;
  name: string;
  sector: string;
  close: number | null;
  change_pct: number | null;
  volume: number | null;
}

export interface PricePoint {
  date: string;
  open: number | null;
  high: number | null;
  low: number | null;
  close: number | null;
  volume: number | null;
  change_pct: number | null;
}

export interface StockDetail {
  symbol: string;
  name: string;
  sector: string;
  prices: PricePoint[];
}

export interface MarketSignal {
  id: number;
  signal_type: string;
  direction: "bullish" | "bearish" | "neutral";
  confidence: number;
  entities: string[];
  sectors: string[];
  summary: string;
  historical_note: string | null;
  raw_headline: string | null;
  is_notified: boolean;
  created_at: string | null;
  source: string | null;
  source_url: string | null;
}

export interface PortfolioSignal {
  direction: string;
  confidence: number;
  summary: string;
  signal_type: string;
  created_at: string | null;
}

export interface Holding {
  id: number;
  symbol: string;
  shares_held: number;
  avg_buy_price: number;
  cost_basis: number;
  current_price: number | null;
  current_value: number | null;
  pnl: number | null;
  pnl_pct: number | null;
  notes: string;
  signals: PortfolioSignal[];
  updated_at: string | null;
}

export interface PortfolioSummary {
  total_cost: number;
  total_value: number | null;
  total_pnl: number | null;
  total_pnl_pct: number | null;
  count: number;
}

export interface SignalStats {
  total_signals: number;
  bullish: number;
  bearish: number;
  total_articles: number;
  relevant_articles: number;
  filter_rate_pct: number | null;
  validated: number;
  correct: number;
  wrong: number;
  accuracy_pct: number | null;
}
