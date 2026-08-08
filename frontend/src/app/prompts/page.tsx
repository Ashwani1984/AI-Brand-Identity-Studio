"use client";

import { useState } from "react";
import Link from "next/link";
import { BrandProjectPicker } from "@/components/brand-picker";
import { UpgradeBanner, InsufficientCreditsHint } from "@/components/upgrade-banner";
import { api, refreshProfile } from "@/lib/api";

type PromptResult = {
  brandName: string;
  kind: string;
  creditCost: number;
  masterPrompt: string;
  tools: Record<string, string>;
  monetizationTip: string;
};

const kinds = [
  { label: "Logo", value: "Logo" },
  { label: "Mockup", value: "Mockup" },
  { label: "Video", value: "Video" },
  { label: "Social", value: "Social" },
  { label: "Landing", value: "Landing" },
  { label: "Packaging", value: "Packaging" },
  { label: "Ads", value: "Ads" },
];

export default function PromptsPage() {
  const [brandId, setBrandId] = useState("");
  const [kind, setKind] = useState("Logo");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<PromptResult | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  const run = async () => {
    if (!brandId) {
      setError("Select a brand project first.");
      return;
    }
    try {
      setLoading(true);
      setError(null);
      const data = await api<PromptResult>("/api/prompts/generate", {
        method: "POST",
        body: JSON.stringify({ brandId, kind }),
      });
      setResult(data);
      await refreshProfile();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed");
    } finally {
      setLoading(false);
    }
  };

  const copy = async (key: string, text: string) => {
    await navigator.clipboard.writeText(text);
    setCopied(key);
    setTimeout(() => setCopied(null), 1200);
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-semibold tracking-tight">Prompt Generator</h2>
        <p className="text-[var(--muted)]">
          Multi-tool prompt packs for Ideogram, Midjourney, Flux, DALL-E, and more - sell-ready creative briefs.
        </p>
      </div>
      <UpgradeBanner
        title="Turn prompts into paid deliverables"
        body="Agencies charge ₹2,000–₹8,000 per prompt suite. Upgrade for higher volume and client white-label exports."
      />
      <div className="card space-y-4">
        <BrandProjectPicker value={brandId} onChange={(b) => setBrandId(b)} />
        <div className="grid gap-3 sm:grid-cols-2">
          <select className="input" value={kind} onChange={(e) => setKind(e.target.value)}>
            {kinds.map((k) => (
              <option key={k.value} value={k.value}>{k.label}</option>
            ))}
          </select>
          <button className="btn" onClick={run} disabled={loading}>
            {loading ? "Generating pack..." : "Generate prompt pack - 1 credit"}
          </button>
        </div>
        <InsufficientCreditsHint message={error || undefined} />
        {error && !error.toLowerCase().includes("credit") && <p className="text-sm text-red-500">{error}</p>}
      </div>

      {result && (
        <div className="space-y-4">
          <div className="card space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <h3 className="text-lg font-semibold">{result.brandName} - {result.kind}</h3>
                <p className="text-xs text-[var(--muted)]">Cost: {result.creditCost} credit</p>
              </div>
              <button className="btn btn-ghost text-xs" onClick={() => copy("master", result.masterPrompt)}>
                {copied === "master" ? "Copied" : "Copy master prompt"}
              </button>
            </div>
            <pre className="overflow-auto rounded-xl bg-black/20 p-3 text-xs whitespace-pre-wrap">{result.masterPrompt}</pre>
            <p className="text-sm text-teal-400">{result.monetizationTip}</p>
          </div>
          <div className="grid gap-3 md:grid-cols-2">
            {Object.entries(result.tools).map(([tool, prompt]) => (
              <div key={tool} className="card space-y-2">
                <div className="flex items-center justify-between">
                  <h4 className="font-semibold capitalize">{tool}</h4>
                  <button className="text-xs text-teal-400" onClick={() => copy(tool, prompt)}>
                    {copied === tool ? "Copied" : "Copy"}
                  </button>
                </div>
                <p className="text-xs leading-relaxed text-[var(--muted)] line-clamp-6">{prompt}</p>
              </div>
            ))}
          </div>
          <div className="card flex flex-wrap items-center justify-between gap-3">
            <p className="text-sm text-[var(--muted)]">Need images from these prompts?</p>
            <div className="flex gap-2">
              <Link href="/logo" className="btn btn-ghost">Logo Generator</Link>
              <Link href="/billing" className="btn">Get more credits</Link>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
