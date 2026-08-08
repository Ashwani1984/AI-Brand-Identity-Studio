"use client";

import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { enqueueAndWait } from "@/lib/jobs";
import { useState } from "react";

type Project = { id: string; name: string; status: string; brandId?: string | null };
type Brand = {
  id: string;
  generatedName?: string;
  tagline?: string;
  brandStory?: string;
  brandVoice?: string;
  businessName: string;
};
type Logo = { id: string; imageUrl?: string; svgContent?: string; style: string; promptText: string };
type Palette = {
  id: string;
  primaryHex: string;
  secondaryHex: string;
  accentHex: string;
  backgroundHex: string;
  typographyHex: string;
};
type Asset = { id: string; kind: string; fileName: string; url: string };

export default function ProjectDetailPage() {
  const params = useParams<{ id: string }>();
  const qc = useQueryClient();
  const [logo, setLogo] = useState<Logo | null>(null);
  const [palette, setPalette] = useState<Palette | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const { data: project } = useQuery({
    queryKey: ["project", params.id],
    queryFn: () => api<Project>(`/api/projects/${params.id}`),
  });

  const { data: brand } = useQuery({
    queryKey: ["brand", project?.brandId],
    enabled: !!project?.brandId,
    queryFn: () => api<Brand>(`/api/brands/${project!.brandId}`),
  });

  const generateBrand = useMutation({
    mutationFn: () => api<Brand>(`/api/brands/${project!.brandId}/generate`, { method: "POST" }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["brand", project?.brandId] });
      setMessage("Brand generated.");
    },
    onError: (e: Error) => setMessage(e.message),
  });

  const generateLogo = useMutation({
    mutationFn: () =>
      enqueueAndWait<Logo>("/api/logos/generate", { brandId: project!.brandId, style: "Minimal" }),
    onSuccess: (data) => {
      setLogo(data);
      setMessage("Logo generated.");
    },
    onError: (e: Error) => setMessage(e.message),
  });

  const generatePalette = useMutation({
    mutationFn: () =>
      api<Palette>("/api/palettes/generate", {
        method: "POST",
        body: JSON.stringify({ brandId: project!.brandId }),
      }),
    onSuccess: (data) => {
      setPalette(data);
      setMessage("Palette generated.");
    },
    onError: (e: Error) => setMessage(e.message),
  });

  const exportZip = useMutation({
    mutationFn: () => enqueueAndWait<Asset>("/api/exports/zip", { projectId: params.id }),
    onSuccess: (data) => {
      setMessage(`Export ready: ${data.fileName}`);
      window.open(`${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5080"}${data.url}`, "_blank");
    },
    onError: (e: Error) => setMessage(e.message),
  });

  if (!project) return <p>Loading project...</p>;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-semibold">{project.name}</h2>
        <p className="text-[var(--muted)]">Status: {project.status}</p>
      </div>
      {message && <p className="text-sm text-teal-500">{message}</p>}
      {!project.brandId ? (
        <p>No brand yet. Use the Brand Wizard.</p>
      ) : (
        <>
          <div className="card space-y-2">
            <h3 className="text-lg font-semibold">Brand</h3>
            <p><strong>{brand?.generatedName || brand?.businessName}</strong></p>
            <p className="text-[var(--muted)]">{brand?.tagline}</p>
            <p className="text-sm">{brand?.brandStory}</p>
            <div className="flex flex-wrap gap-2 pt-2">
              <button className="btn" onClick={() => generateBrand.mutate()} disabled={generateBrand.isPending}>
                Generate brand
              </button>
              <button className="btn btn-ghost" onClick={() => generateLogo.mutate()} disabled={generateLogo.isPending}>
                Generate logo
              </button>
              <button className="btn btn-ghost" onClick={() => generatePalette.mutate()} disabled={generatePalette.isPending}>
                Generate palette
              </button>
              <button className="btn btn-ghost" onClick={() => exportZip.mutate()} disabled={exportZip.isPending}>
                Export ZIP
              </button>
            </div>
          </div>
          {logo && (
            <div className="card">
              <h3 className="mb-2 font-semibold">Logo</h3>
              {logo.svgContent ? (
                <div dangerouslySetInnerHTML={{ __html: logo.svgContent }} className="max-w-xs" />
              ) : (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={`${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5080"}${logo.imageUrl}`} alt="logo" className="max-w-xs" />
              )}
            </div>
          )}
          {palette && (
            <div className="card">
              <h3 className="mb-3 font-semibold">Palette</h3>
              <div className="flex flex-wrap gap-3">
                {[palette.primaryHex, palette.secondaryHex, palette.accentHex, palette.backgroundHex, palette.typographyHex].map((c) => (
                  <div key={c} className="text-center text-xs">
                    <div className="h-12 w-12 rounded-lg border" style={{ background: c }} />
                    <div className="mt-1">{c}</div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
