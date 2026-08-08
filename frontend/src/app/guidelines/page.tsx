"use client";

import { useState } from "react";
import Link from "next/link";
import { BrandProjectPicker } from "@/components/brand-picker";
import { UpgradeBanner, InsufficientCreditsHint } from "@/components/upgrade-banner";
import { api, refreshProfile } from "@/lib/api";
import { assetUrl } from "@/components/generator-studio";

type GuidelinesResult = {
  brandName: string;
  creditCost: number;
  markdownUrl: string;
  htmlUrl: string;
  markdown: string;
  sections: string[];
  upsell: string;
};

export default function GuidelinesPage() {
  const [brandId, setBrandId] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<GuidelinesResult | null>(null);

  const run = async () => {
    if (!brandId) {
      setError("Select a brand project first.");
      return;
    }
    try {
      setLoading(true);
      setError(null);
      const data = await api<GuidelinesResult>("/api/guidelines/generate", {
        method: "POST",
        body: JSON.stringify({ brandId }),
      });
      setResult(data);
      await refreshProfile();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-semibold tracking-tight">Brand Guidelines</h2>
        <p className="text-[var(--muted)]">
          Client-ready brand book: voice, logo rules, color, type, photography - Markdown + printable HTML.
        </p>
      </div>
      <UpgradeBanner
        title="Sell brand books as a premium SKU"
        body="Position guidelines as a ₹4,999–₹14,999 client deliverable. Pro unlocks white-label PDF and multi-brand kits."
      />
      <div className="card space-y-4">
        <BrandProjectPicker value={brandId} onChange={(b) => setBrandId(b)} />
        <button className="btn" onClick={run} disabled={loading}>
          {loading ? "Building guidelines..." : "Generate client brand book - 3 credits"}
        </button>
        <InsufficientCreditsHint message={error || undefined} />
        {error && !error.toLowerCase().includes("credit") && <p className="text-sm text-red-500">{error}</p>}
      </div>

      {result && (
        <div className="space-y-4">
          <div className="card space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h3 className="text-xl font-semibold">{result.brandName} Brand Book</h3>
                <p className="text-xs text-[var(--muted)]">Cost: {result.creditCost} credits</p>
              </div>
              <div className="flex flex-wrap gap-2">
                <a className="btn btn-ghost" href={assetUrl(result.htmlUrl)} target="_blank">Open HTML</a>
                <a className="btn" href={assetUrl(result.markdownUrl)} target="_blank">Download MD</a>
              </div>
            </div>
            <div className="flex flex-wrap gap-2">
              {result.sections.map((s) => (
                <span key={s} className="rounded-full border border-white/10 px-3 py-1 text-xs text-[var(--muted)]">{s}</span>
              ))}
            </div>
            <p className="text-sm text-teal-400">{result.upsell}</p>
          </div>
          <div className="card">
            <h4 className="mb-3 font-semibold">Preview</h4>
            <pre className="max-h-[420px] overflow-auto rounded-xl bg-black/20 p-4 text-xs whitespace-pre-wrap">{result.markdown}</pre>
          </div>
          <div className="card flex flex-wrap items-center justify-between gap-3">
            <p className="text-sm text-[var(--muted)]">Package this with logos & palette into a Pro ZIP for clients.</p>
            <div className="flex gap-2">
              <Link href="/downloads" className="btn">Go to Downloads</Link>
              <Link href="/billing" className="btn btn-ghost">Upgrade</Link>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
