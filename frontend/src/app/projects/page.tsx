"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";

type Project = {
  id: string;
  name: string;
  status: string;
  brandId?: string | null;
  businessName?: string | null;
};

export default function ProjectsPage() {
  const { data = [], isLoading } = useQuery({
    queryKey: ["projects"],
    queryFn: () => api<Project[]>("/api/projects"),
  });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-3xl font-semibold">Projects</h2>
        <Link href="/wizard" className="btn">New project</Link>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        {isLoading && <p>Loading...</p>}
        {data.map((p) => (
          <Link key={p.id} href={`/projects/${p.id}`} className="card block transition hover:border-teal-500/40">
            <div className="text-lg font-semibold">{p.name}</div>
            <div className="mt-1 text-sm text-[var(--muted)]">{p.businessName || "No brand yet"} - {p.status}</div>
          </Link>
        ))}
      </div>
    </div>
  );
}
