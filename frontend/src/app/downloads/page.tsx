"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { api, refreshProfile } from "@/lib/api";
import { enqueueAndWait } from "@/lib/jobs";
import { BrandProjectPicker } from "@/components/brand-picker";
import { useMemo, useState } from "react";
import { assetUrl } from "@/components/generator-studio";
import { UpgradeBanner, InsufficientCreditsHint } from "@/components/upgrade-banner";
import Link from "next/link";

type Asset = { id: string; kind: string; fileName: string; url: string; byteSize: number; createdAtUtc?: string };

export default function DownloadsPage() {
  const [brandId, setBrandId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [filter, setFilter] = useState("all");
  const [msg, setMsg] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const { data = [], refetch, isLoading } = useQuery({
    queryKey: ["downloads"],
    queryFn: () => api<Asset[]>("/api/downloads"),
  });

  const filtered = useMemo(() => {
    if (filter === "all") return data;
    if (filter === "exports") return data.filter((a) => a.kind.toLowerCase().includes("export"));
    if (filter === "guidelines") return data.filter((a) => a.kind.toLowerCase().includes("guideline"));
    return data.filter((a) => a.kind.toLowerCase().includes(filter));
  }, [data, filter]);

  const zip = useMutation({
    mutationFn: (packageType: string) =>
      enqueueAndWait<Asset>("/api/downloads/zip", { projectId, packageType }),
    onSuccess: async (a) => {
      setError(null);
      setMsg(`${a.kind === "ExportZipPro" ? "Pro" : "Standard"} ZIP ready: ${a.fileName}`);
      await refreshProfile();
      refetch();
      window.open(assetUrl(a.url), "_blank");
    },
    onError: (e: Error) => {
      setMsg(null);
      setError(e.message);
    },
  });

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-semibold tracking-tight">Downloads / Export Center</h2>
        <p className="text-[var(--muted)]">
          Package client-ready brand kits. Standard is free - Pro ZIP includes guidelines, landing, and mockups (5 credits).
        </p>
      </div>
      <UpgradeBanner
        title="Monetize every export"
        body="Charge clients for Pro brand kits or include them in Builder/Scale subscriptions. Higher plans = sticky recurring revenue."
      />

      <div className="grid gap-4 md:grid-cols-2">
        <div className="card space-y-3 border-teal-500/20">
          <h3 className="text-lg font-semibold">Standard Kit</h3>
          <p className="text-sm text-[var(--muted)]">Brand JSON, palette CSS/JSON, logos, README.</p>
          <p className="text-2xl font-semibold">Free</p>
          <BrandProjectPicker
            value={brandId}
            onChange={(b, p) => {
              setBrandId(b);
              setProjectId(p);
            }}
          />
          <button className="btn w-full" disabled={!projectId || zip.isPending} onClick={() => zip.mutate("Standard")}>
            {zip.isPending ? "Packaging..." : "Export Standard ZIP"}
          </button>
        </div>
        <div className="card space-y-3 border-amber-500/30 bg-gradient-to-br from-teal-500/10 to-transparent">
          <div className="text-xs uppercase tracking-wider text-teal-400">Best for client delivery</div>
          <h3 className="text-lg font-semibold">Pro Client Kit</h3>
          <p className="text-sm text-[var(--muted)]">Everything in Standard + guidelines, landing page, mockups, and more assets.</p>
          <p className="text-2xl font-semibold">5 credits</p>
          <button className="btn w-full" disabled={!projectId || zip.isPending} onClick={() => zip.mutate("Pro")}>
            {zip.isPending ? "Packaging..." : "Export Pro ZIP"}
          </button>
          <Link href="/billing" className="text-center text-sm text-teal-400 hover:underline">
            Need credits? Upgrade plan →
          </Link>
        </div>
      </div>

      <InsufficientCreditsHint message={error || undefined} />
      {msg && <p className="text-sm text-teal-500">{msg}</p>}
      {error && !error.toLowerCase().includes("credit") && <p className="text-sm text-red-500">{error}</p>}

      <div className="card space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h3 className="font-semibold">Your files</h3>
          <select className="input max-w-xs" value={filter} onChange={(e) => setFilter(e.target.value)}>
            <option value="all">All assets</option>
            <option value="exports">Exports</option>
            <option value="logo">Logos</option>
            <option value="guidelines">Guidelines</option>
            <option value="landing">Landing</option>
            <option value="mockup">Mockups</option>
          </select>
        </div>
        {isLoading ? (
          <p className="text-sm text-[var(--muted)]">Loading...</p>
        ) : filtered.length === 0 ? (
          <div className="rounded-xl border border-dashed border-white/15 p-6 text-center">
            <p className="text-sm text-[var(--muted)]">No files yet. Generate a brand, then export a kit.</p>
            <div className="mt-3 flex justify-center gap-2">
              <Link href="/wizard" className="btn">Brand Wizard</Link>
              <Link href="/guidelines" className="btn btn-ghost">Guidelines</Link>
            </div>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="text-[var(--muted)]">
                <tr>
                  <th className="pb-2">Kind</th>
                  <th className="pb-2">File</th>
                  <th className="pb-2">Size</th>
                  <th className="pb-2">Download</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((a) => (
                  <tr key={a.id} className="border-t border-white/5">
                    <td className="py-3">
                      <span className="rounded-full border border-white/10 px-2 py-0.5 text-xs">{a.kind}</span>
                    </td>
                    <td>{a.fileName}</td>
                    <td>{Math.max(1, Math.round(a.byteSize / 1024))} KB</td>
                    <td>
                      <a className="font-medium text-teal-500 hover:underline" href={assetUrl(a.url)} target="_blank">
                        Download
                      </a>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
