"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { FolderOpen } from "lucide-react";

export type ProjectOption = {
  id: string;
  name: string;
  brandId?: string | null;
  businessName?: string | null;
};

export function useProjectsWithBrands() {
  return useQuery({ queryKey: ["projects"], queryFn: () => api<ProjectOption[]>("/api/projects") });
}

export function BrandProjectPicker({ value, onChange }: { value: string; onChange: (brandId: string, projectId: string) => void }) {
  const { data = [], isLoading } = useProjectsWithBrands();
  const withBrand = data.filter((p) => p.brandId);

  if (isLoading) return <div className="rounded-2xl border border-[var(--border)] p-4 text-sm text-[var(--muted)]">Loading brand projects...</div>;
  if (withBrand.length === 0) {
    return (
      <div className="rounded-2xl border border-amber-400/30 bg-amber-400/10 p-4">
        <div className="font-semibold text-amber-700 dark:text-amber-200">No generated brand yet</div>
        <p className="mt-1 text-sm text-[var(--muted)]">Create a brand in the wizard before using this generator.</p>
        <Link href="/wizard" className="btn mt-3 min-h-0 px-4 py-2 text-xs">Open Brand Wizard</Link>
      </div>
    );
  }

  return (
    <label className="block">
      <span className="mb-2 flex items-center gap-2 text-sm font-semibold text-[var(--muted)]"><FolderOpen className="h-4 w-4" /> Brand project</span>
      <select
        className="input"
        value={value}
        onChange={(e) => {
          const p = withBrand.find((x) => x.brandId === e.target.value);
          if (p?.brandId) onChange(p.brandId, p.id);
        }}
      >
        <option value="">Select brand project</option>
        {withBrand.map((p) => (
          <option key={p.id} value={p.brandId!}>{p.businessName || p.name}</option>
        ))}
      </select>
    </label>
  );
}
