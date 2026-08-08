"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";

type Analytics = {
  projects: number;
  brands: number;
  logos: number;
  assets: number;
  credits: number;
  recentLedger: Array<{ delta: number; reason: string; createdAtUtc: string }>;
};

export default function AnalyticsPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ["analytics"],
    queryFn: () => api<Analytics>("/api/analytics"),
  });

  if (isLoading) return <p>Loading analytics...</p>;
  if (error) return <p className="text-red-500">{(error as Error).message}</p>;

  return (
    <div className="space-y-6">
      <h2 className="text-3xl font-semibold">Analytics</h2>
      <div className="grid gap-4 md:grid-cols-5">
        {[
          ["Projects", data?.projects],
          ["Brands", data?.brands],
          ["Logos", data?.logos],
          ["Assets", data?.assets],
          ["Credits", data?.credits],
        ].map(([label, value]) => (
          <div key={String(label)} className="card">
            <div className="text-sm text-[var(--muted)]">{label}</div>
            <div className="mt-2 text-3xl font-semibold">{value ?? 0}</div>
          </div>
        ))}
      </div>
      <div className="card">
        <h3 className="mb-3 font-semibold">Credit activity</h3>
        <ul className="space-y-2 text-sm">
          {(data?.recentLedger || []).map((r, i) => (
            <li key={i} className="flex justify-between border-b border-white/5 pb-2">
              <span>{r.reason}</span>
              <span className={r.delta < 0 ? "text-red-400" : "text-teal-400"}>
                {r.delta > 0 ? `+${r.delta}` : r.delta}
              </span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
