"use client";
import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";

interface Stats {
  total_signals: number;
  bullish: number;
  bearish: number;
  total_articles: number;
  relevant_articles: number;
  validated: number;
  correct: number;
  wrong: number;
  accuracy_pct: number | null;
}

interface Briefing {
  id: number;
  briefing_date: string;
  content: string;
  signal_count: number;
  accuracy_pct: number | null;
  is_pushed: boolean;
}

type NotifState = "idle" | "subscribed" | "denied" | "unsupported";

function StatCard({ label, value, sub }: { label: string; value: string | number; sub?: string }) {
  return (
    <div className="bg-surface border border-border rounded-xl p-4">
      <div className="mono text-2xs tracking-widest uppercase text-text3 mb-2">{label}</div>
      <div className="text-2xl font-bold text-text1 mono tabular">{value}</div>
      {sub && <div className="text-xs text-text2 mt-1">{sub}</div>}
    </div>
  );
}

export default function AdminPage() {
  const [stats, setStats] = useState<Stats | null>(null);
  const [briefings, setBriefings] = useState<Briefing[]>([]);
  const [notifState, setNotifState] = useState<NotifState>("idle");
  const [subCount, setSubCount] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [msg, setMsg] = useState("");

  const loadData = useCallback(async () => {
    try {
      const [s, b, sc] = await Promise.all([
        api.get<Stats>("/api/signals/stats"),
        api.get<{ briefings: Briefing[] }>("/api/signals/briefings"),
        api.get<{ active_subscribers: number }>("/api/notifications/subscribers/count"),
      ]);
      setStats(s);
      setBriefings(b.briefings || []);
      setSubCount(sc.active_subscribers);
    } catch (e) {
      console.error(e);
    }
  }, []);

  useEffect(() => { loadData(); }, [loadData]);

  useEffect(() => {
    if (!("Notification" in window) || !("serviceWorker" in navigator)) {
      setNotifState("unsupported");
      return;
    }
    if (Notification.permission === "denied") setNotifState("denied");
    else if (Notification.permission === "granted") setNotifState("subscribed");
  }, []);

  async function subscribeWebPush() {
    setLoading(true);
    setMsg("");
    try {
      const reg = await navigator.serviceWorker.register("/sw.js");
      await navigator.serviceWorker.ready;

      const keyResp = await api.get<{ public_key: string }>("/api/notifications/vapid-public-key");
      const perm = await Notification.requestPermission();
      if (perm !== "granted") { setNotifState("denied"); setMsg("Notifications blocked in browser."); return; }

      const sub = await reg.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: _urlBase64ToUint8Array(keyResp.public_key),
      });

      const json = sub.toJSON();
      await api.post("/api/notifications/subscribe", {
        endpoint: json.endpoint,
        keys: { p256dh: json.keys!.p256dh, auth: json.keys!.auth },
      });

      setNotifState("subscribed");
      setMsg("✅ Web Push enabled! You'll get alerts for high-confidence signals.");
      loadData();
    } catch (e: any) {
      setMsg(`Failed: ${e.message}`);
    } finally {
      setLoading(false);
    }
  }

  async function triggerFetch() {
    setLoading(true);
    setMsg("");
    try {
      await api.post("/api/signals/trigger", {});
      setMsg("News fetch queued — check back in 60s");
      setTimeout(loadData, 10000);
    } catch { setMsg("Failed to queue fetch"); }
    finally { setLoading(false); }
  }

  async function triggerBriefing() {
    setLoading(true);
    setMsg("");
    try {
      await api.post("/api/signals/briefing/trigger", {});
      setMsg("Briefing generation queued — check back in 30s");
      setTimeout(loadData, 15000);
    } catch { setMsg("Failed to queue briefing"); }
    finally { setLoading(false); }
  }

  async function triggerValidation() {
    setLoading(true);
    setMsg("");
    try {
      await api.post("/api/signals/validate/trigger", {});
      setMsg("Validation queued");
      setTimeout(loadData, 10000);
    } catch { setMsg("Failed to queue validation"); }
    finally { setLoading(false); }
  }

  return (
    <div className="min-h-screen bg-bg text-text1 p-6 max-w-5xl mx-auto">
      <div className="mb-8">
        <div className="mono text-2xs tracking-widest uppercase text-text3 mb-1">PSX Intelligence</div>
        <h1 className="text-2xl font-bold text-text1">Admin Panel</h1>
      </div>

      {/* Stats */}
      {stats && (
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-8">
          <StatCard label="Total Signals" value={stats.total_signals} />
          <StatCard label="Accuracy" value={stats.accuracy_pct != null ? `${stats.accuracy_pct}%` : "—"} sub={`${stats.correct}/${stats.validated} validated`} />
          <StatCard label="Articles" value={stats.total_articles} sub={`${stats.relevant_articles} relevant`} />
          <StatCard label="Push Subs" value={subCount ?? "—"} sub="active subscribers" />
        </div>
      )}

      {/* Actions */}
      <div className="bg-surface border border-border rounded-xl p-5 mb-6">
        <div className="mono text-2xs tracking-widest uppercase text-text3 mb-4">Actions</div>
        <div className="flex flex-wrap gap-3">
          <button
            onClick={triggerFetch}
            disabled={loading}
            className="px-4 py-2 bg-accent/15 text-accent border border-accent/40 rounded-lg mono text-sm hover:bg-accent/25 transition-colors disabled:opacity-50"
          >
            Fetch News Now
          </button>
          <button
            onClick={triggerBriefing}
            disabled={loading}
            className="px-4 py-2 bg-surface2 text-text1 border border-border rounded-lg mono text-sm hover:border-border-b transition-colors disabled:opacity-50"
          >
            Generate Briefing
          </button>
          <button
            onClick={triggerValidation}
            disabled={loading}
            className="px-4 py-2 bg-surface2 text-text1 border border-border rounded-lg mono text-sm hover:border-border-b transition-colors disabled:opacity-50"
          >
            Run Validator
          </button>
          {notifState !== "subscribed" && notifState !== "unsupported" && (
            <button
              onClick={subscribeWebPush}
              disabled={loading || notifState === "denied"}
              className="px-4 py-2 bg-amber/15 text-amber border border-amber/40 rounded-lg mono text-sm hover:bg-amber/25 transition-colors disabled:opacity-50"
            >
              {notifState === "denied" ? "Notifications Blocked" : "Enable Push Alerts"}
            </button>
          )}
          {notifState === "subscribed" && (
            <span className="px-4 py-2 bg-accent/10 text-accent border border-accent/30 rounded-lg mono text-sm">
              ✓ Push Alerts Active
            </span>
          )}
        </div>
        {msg && <p className="text-sm text-text2 mt-3 mono">{msg}</p>}
      </div>

      {/* Schedule */}
      <div className="bg-surface border border-border rounded-xl p-5 mb-6">
        <div className="mono text-2xs tracking-widest uppercase text-text3 mb-4">Auto Schedule</div>
        <div className="flex flex-col gap-2 text-sm">
          {[
            { time: "Every 30 min", task: "News fetch + Claude signal extraction" },
            { time: "Mon–Fri 08:00 PKT", task: "Morning market briefing (Claude Sonnet)" },
            { time: "Mon–Fri 15:45 PKT", task: "Daily stock price fetch" },
            { time: "Mon–Fri 16:30 PKT", task: "Signal accuracy validator" },
          ].map(r => (
            <div key={r.task} className="flex gap-4 items-baseline">
              <span className="mono text-accent text-xs w-36 flex-shrink-0">{r.time}</span>
              <span className="text-text2">{r.task}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Briefings */}
      {briefings.length > 0 && (
        <div className="bg-surface border border-border rounded-xl p-5">
          <div className="mono text-2xs tracking-widest uppercase text-text3 mb-4">Recent Briefings</div>
          {briefings.slice(0, 3).map(b => (
            <div key={b.id} className="mb-4 pb-4 border-b border-border last:border-0">
              <div className="flex items-center gap-3 mb-2">
                <span className="mono text-xs text-text3">
                  {new Date(b.briefing_date).toLocaleDateString("en-PK", { weekday: "short", month: "short", day: "numeric" })}
                </span>
                <span className="mono text-xs text-text2">{b.signal_count} signals</span>
                {b.accuracy_pct && <span className="mono text-xs text-accent">{b.accuracy_pct}% accuracy</span>}
                {b.is_pushed && <span className="mono text-xs text-text3">pushed ✓</span>}
              </div>
              <p className="text-sm text-text2 leading-relaxed">{b.content}</p>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function _urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = "=".repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, "+").replace(/_/g, "/");
  const raw = window.atob(base64);
  return Uint8Array.from([...raw].map(c => c.charCodeAt(0)));
}
