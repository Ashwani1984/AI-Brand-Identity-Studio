"use client";

import { useState } from "react";
import Link from "next/link";
import { BrandProjectPicker } from "@/components/brand-picker";
import { UpgradeBanner, InsufficientCreditsHint } from "@/components/upgrade-banner";
import { api, refreshProfile } from "@/lib/api";
import { enqueueAndWait } from "@/lib/jobs";
import { useGenerationHub } from "@/lib/signalr";
import { assetUrl } from "@/components/generator-studio";

type Logo = {
  id: string;
  brandId: string;
  style: string;
  promptText: string;
  imageUrl?: string;
  svgContent?: string;
  provider: string;
};

const STYLES = ["Minimal", "Luxury", "Modern", "Vintage", "Mascot", "Tech", "Geometric", "Gradient", "Flat", "Monogram"];

function downloadText(filename: string, content: string, type: string) {
  const blob = new Blob([content], { type });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

async function downloadSvgAsPng(svg: string, filename: string) {
  const svgBlob = new Blob([svg], { type: "image/svg+xml;charset=utf-8" });
  const url = URL.createObjectURL(svgBlob);
  const img = new Image();
  await new Promise<void>((resolve, reject) => {
    img.onload = () => resolve();
    img.onerror = reject;
    img.src = url;
  });
  const canvas = document.createElement("canvas");
  canvas.width = 1024;
  canvas.height = 1024;
  const ctx = canvas.getContext("2d");
  if (!ctx) return;
  ctx.fillStyle = "#ffffff";
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  ctx.drawImage(img, 0, 0, 1024, 1024);
  URL.revokeObjectURL(url);
  const png = canvas.toDataURL("image/png");
  const a = document.createElement("a");
  a.href = png;
  a.download = filename;
  a.click();
}

export default function LogoPage() {
  const [brandId, setBrandId] = useState("");
  const [style, setStyle] = useState("Minimal");
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [current, setCurrent] = useState<Logo | null>(null);
  const [history, setHistory] = useState<Logo[]>([]);
  useGenerationHub();

  const loadHistory = async (id: string) => {
    const rows = await api<Logo[]>(`/api/logos?brandId=${id}`);
    setHistory(rows);
    if (rows[0]) setCurrent(rows[0]);
  };

  const generate = async () => {
    if (!brandId) {
      setError("Select a brand project.");
      return;
    }
    try {
      setLoading(true);
      setError(null);
      const styledNote = notes.trim() ? `${style} - ${notes.trim()}` : style;
      const logo = await enqueueAndWait<Logo>("/api/logos/generate", { brandId, style: styledNote });
      setCurrent(logo);
      await loadHistory(brandId);
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
        <h2 className="text-3xl font-semibold tracking-tight">Logo Generator</h2>
        <p className="text-[var(--muted)]">
          Enterprise logo studio: SVG + transparent PNG export, vector prompts, style presets, regenerate & history.
        </p>
      </div>
      <UpgradeBanner body="Ship unlimited logo variants to clients on Builder/Scale plans." />

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="card space-y-4">
          <BrandProjectPicker value={brandId} onChange={(b) => {
            setBrandId(b);
            void loadHistory(b).catch(() => undefined);
          }} />
          <label className="block text-sm text-[var(--muted)]">Style</label>
          <select className="input" value={style} onChange={(e) => setStyle(e.target.value)} aria-label="Logo style">
            {STYLES.map((s) => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
          <label className="block text-sm text-[var(--muted)]">Edit direction (optional)</label>
          <textarea
            className="input min-h-24"
            placeholder="e.g. sharper corners, add monogram M, more luxury serif feel"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            aria-label="Logo edit notes"
          />
          <button className="btn w-full" onClick={generate} disabled={loading}>
            {loading ? "Generating..." : current ? "Regenerate logo - 8 credits" : "Generate logo - 8 credits"}
          </button>
          <InsufficientCreditsHint message={error || undefined} />
          {error && !error.toLowerCase().includes("credit") && <p className="text-sm text-red-500">{error}</p>}
        </div>

        <div className="card space-y-3">
          <h3 className="font-semibold">Preview</h3>
          {!current ? (
            <p className="text-sm text-[var(--muted)]">Generate a logo to preview exports.</p>
          ) : (
            <>
              <div className="rounded-xl bg-white p-6">
                {current.svgContent ? (
                  <div dangerouslySetInnerHTML={{ __html: current.svgContent }} className="mx-auto max-w-[240px]" />
                ) : (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={assetUrl(current.imageUrl)} alt={`${current.style} logo`} className="mx-auto max-w-[240px]" />
                )}
              </div>
              <p className="text-xs text-[var(--muted)]">{current.style} - {current.provider}</p>
              <div className="flex flex-wrap gap-2">
                {current.svgContent && (
                  <>
                    <button className="btn btn-ghost" onClick={() => downloadText(`logo-${current.id}.svg`, current.svgContent!, "image/svg+xml")}>
                      Export SVG
                    </button>
                    <button className="btn btn-ghost" onClick={() => downloadSvgAsPng(current.svgContent!, `logo-${current.id}.png`)}>
                      Export PNG
                    </button>
                  </>
                )}
                {current.imageUrl && !current.svgContent && (
                  <a className="btn btn-ghost" href={assetUrl(current.imageUrl)} target="_blank">Open asset</a>
                )}
                <button className="btn btn-ghost" onClick={() => navigator.clipboard.writeText(current.promptText)}>
                  Copy vector prompt
                </button>
              </div>
              <pre className="max-h-40 overflow-auto rounded-xl bg-black/20 p-3 text-xs whitespace-pre-wrap">{current.promptText}</pre>
            </>
          )}
        </div>
      </div>

      <div className="card">
        <h3 className="mb-3 font-semibold">Version history</h3>
        {history.length === 0 ? (
          <p className="text-sm text-[var(--muted)]">No logos yet for this brand.</p>
        ) : (
          <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {history.map((l) => (
              <li key={l.id}>
                <button
                  className={`w-full rounded-xl border p-3 text-left transition hover:border-teal-500/40 ${current?.id === l.id ? "border-teal-500/50" : "border-white/10"}`}
                  onClick={() => setCurrent(l)}
                >
                  <div className="mb-2 rounded-lg bg-white p-2">
                    {l.svgContent ? (
                      <div dangerouslySetInnerHTML={{ __html: l.svgContent }} className="mx-auto max-h-24 overflow-hidden" />
                    ) : (
                      // eslint-disable-next-line @next/next/no-img-element
                      <img src={assetUrl(l.imageUrl)} alt={l.style} className="mx-auto max-h-24" />
                    )}
                  </div>
                  <div className="text-xs text-[var(--muted)]">{l.style}</div>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="card flex flex-wrap gap-2">
        <Link href="/mockups" className="btn btn-ghost">Next: Mockups</Link>
        <Link href="/downloads" className="btn">Export kit</Link>
        <Link href="/workflow" className="btn btn-ghost">Full workflow</Link>
      </div>
    </div>
  );
}
