"use client";

import { useState } from "react";
import { BrandProjectPicker } from "@/components/brand-picker";
import { refreshProfile, API_URL } from "@/lib/api";
import { Alert, Button, Field, Select } from "@/components/ui";
import { ArrowRight, Copy, Sparkles } from "lucide-react";

type Props = {
  title: string;
  description: string;
  actionLabel: string;
  run: (brandId: string, projectId: string, extra?: string) => Promise<unknown>;
  extraOptions?: { label: string; value: string }[];
  renderResult?: (data: unknown) => React.ReactNode;
};

export function GeneratorStudio({ title, description, actionLabel, run, extraOptions, renderResult }: Props) {
  const [brandId, setBrandId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [extra, setExtra] = useState(extraOptions?.[0]?.value || "");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<unknown>(null);
  const [copied, setCopied] = useState(false);

  const onRun = async () => {
    if (!brandId) {
      setError("Select a brand project.");
      return;
    }
    try {
      setLoading(true);
      setError(null);
      const data = await run(brandId, projectId, extra);
      setResult(data);
      await refreshProfile();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed");
    } finally {
      setLoading(false);
    }
  };

  const copyJson = async () => {
    await navigator.clipboard.writeText(JSON.stringify(result, null, 2));
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <div className="space-y-7">
      <section className="relative overflow-hidden rounded-[2rem] border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl shadow-slate-950/5 md:p-8">
        <div className="absolute -right-16 -top-20 h-56 w-56 rounded-full bg-teal-500/10 blur-3xl" />
        <div className="relative flex flex-wrap items-end justify-between gap-4">
          <div>
            <div className="kicker">AI Generator</div>
            <h1 className="page-title mt-3">{title}</h1>
            <p className="mt-4 max-w-2xl text-base leading-7 text-[var(--muted)]">{description}</p>
          </div>
          <div className="rounded-full border border-[var(--border)] bg-[var(--card-soft)] px-4 py-2 text-xs font-bold text-[var(--muted)]">
            Enterprise module
          </div>
        </div>
      </section>

      <div className="grid gap-6 lg:grid-cols-[0.72fr_1fr]">
        <div className="card space-y-5">
          <div className="flex items-center gap-3">
            <div className="grid h-10 w-10 place-items-center rounded-2xl bg-teal-500/10 text-teal-600 dark:text-teal-300">
              <Sparkles className="h-5 w-5" />
            </div>
            <div>
              <h2 className="font-black tracking-tight">Generation setup</h2>
              <p className="text-xs text-[var(--muted)]">Choose brand and options.</p>
            </div>
          </div>
          <BrandProjectPicker
            value={brandId}
            onChange={(b, p) => {
              setBrandId(b);
              setProjectId(p);
            }}
          />
          {extraOptions && (
            <Field label="Output style">
              <Select value={extra} onChange={(e) => setExtra(e.target.value)} aria-label="Output style">
                {extraOptions.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </Select>
            </Field>
          )}
          {error && (
            <Alert tone="error" actionHref="/billing" actionLabel="Upgrade credits">
              {error}
            </Alert>
          )}
          <Button className="w-full" onClick={onRun} disabled={loading}>
            {loading ? "Generating..." : actionLabel} {!loading && <ArrowRight className="h-4 w-4" />}
          </Button>
        </div>

        <div className="card min-h-[22rem] space-y-4">
          <div className="flex items-center justify-between gap-3">
            <div>
              <h2 className="font-black tracking-tight">Output preview</h2>
              <p className="text-xs text-[var(--muted)]">Generated content appears here.</p>
            </div>
            {result != null && (
              <Button variant="ghost" size="sm" onClick={copyJson}>
                <Copy className="h-3.5 w-3.5" /> {copied ? "Copied" : "Copy JSON"}
              </Button>
            )}
          </div>
          {result == null ? (
            <div className="grid min-h-[16rem] place-items-center rounded-[1.25rem] border border-dashed border-[var(--border)] bg-[var(--card-soft)] text-center">
              <div>
                <Sparkles className="mx-auto h-8 w-8 text-teal-500" />
                <p className="mt-3 text-sm font-semibold">Ready to generate</p>
                <p className="mt-1 text-xs text-[var(--muted)]">Select a brand project and run this module.</p>
              </div>
            </div>
          ) : renderResult ? (
            renderResult(result)
          ) : (
            <pre className="max-h-[32rem] overflow-auto rounded-2xl bg-slate-950 p-4 text-xs leading-6 text-slate-100 whitespace-pre-wrap">
              {JSON.stringify(result, null, 2)}
            </pre>
          )}
        </div>
      </div>
    </div>
  );
}

export function assetUrl(path?: string) {
  if (!path) return "#";
  if (path.startsWith("http")) return path;
  return `${API_URL}${path}`;
}
