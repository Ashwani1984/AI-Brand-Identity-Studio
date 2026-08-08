"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { motion } from "framer-motion";
import { links } from "@/components/nav-links";
import { ArrowRight, Box, CreditCard, Image, Layers3, Sparkles } from "lucide-react";

type Project = {
  id: string;
  name: string;
  status: string;
  createdAtUtc: string;
  brandId?: string | null;
  businessName?: string | null;
};

type Analytics = { projects: number; brands: number; logos: number; assets: number; credits: number };

const icons = [CreditCard, Layers3, Sparkles, Image, Box];

export default function DashboardPage() {
  const { data = [], isLoading } = useQuery({
    queryKey: ["projects"],
    queryFn: () => api<Project[]>("/api/projects"),
  });
  const analytics = useQuery({
    queryKey: ["analytics"],
    queryFn: () => api<Analytics>("/api/analytics"),
  });

  const quick = links.filter((l) => !["/dashboard", "/users", "/admin"].includes(l.href)).slice(0, 12);
  const stats = [
    { label: "Credits", value: analytics.data?.credits ?? "-", note: "Available balance" },
    { label: "Projects", value: analytics.data?.projects ?? data.length, note: "Brand workspaces" },
    { label: "Brands", value: analytics.data?.brands ?? 0, note: "Generated identities" },
    { label: "Logos", value: analytics.data?.logos ?? 0, note: "Visual marks" },
    { label: "Assets", value: analytics.data?.assets ?? 0, note: "Downloads ready" },
  ];

  return (
    <div className="space-y-8">
      <section className="relative overflow-hidden rounded-[2rem] border border-[var(--border)] bg-[#07111f] p-6 text-white shadow-2xl shadow-slate-950/10 md:p-8">
        <div className="absolute inset-0 surface-grid opacity-10" />
        <div className="absolute -right-24 -top-24 h-72 w-72 rounded-full bg-teal-400/20 blur-3xl" />
        <div className="relative z-10 flex flex-col justify-between gap-8 lg:flex-row lg:items-end">
          <div>
            <p className="kicker text-teal-300">Enterprise workspace</p>
            <h1 className="mt-3 max-w-3xl text-4xl font-black leading-[0.98] tracking-[-0.055em] md:text-6xl">
              Launch a complete brand system from one dashboard.
            </h1>
            <p className="mt-5 max-w-2xl text-base leading-7 text-slate-300">
              Create a brand, generate identity assets, package client-ready exports, and monetize with Pro kits and subscriptions.
            </p>
          </div>
          <div className="flex flex-wrap gap-3">
            <Link href="/wizard" className="btn">
              Create brand <ArrowRight className="h-4 w-4" />
            </Link>
            <Link href="/downloads" className="btn btn-ghost border-white/15 bg-white/[0.06] text-white">
              Export center
            </Link>
          </div>
        </div>
      </section>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {stats.map((s, i) => {
          const Icon = icons[i];
          return (
            <motion.div key={s.label} className="card metric-card" initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: i * 0.04 }}>
              <div className="relative z-10 flex items-start justify-between">
                <div>
                  <div className="text-sm font-medium text-[var(--muted)]">{s.label}</div>
                  <div className="mt-3 text-4xl font-black tracking-tight">{s.value}</div>
                  <div className="mt-2 text-xs text-[var(--muted)]">{s.note}</div>
                </div>
                <div className="grid h-11 w-11 place-items-center rounded-2xl bg-teal-500/10 text-teal-600 dark:text-teal-300">
                  <Icon className="h-5 w-5" />
                </div>
              </div>
            </motion.div>
          );
        })}
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_0.8fr]">
        <section className="card">
          <div className="mb-5 flex items-center justify-between gap-3">
            <div>
              <h2 className="text-xl font-black tracking-tight">Quick launch</h2>
              <p className="text-sm text-[var(--muted)]">Jump directly into revenue-generating brand modules.</p>
            </div>
            <Link href="/workflow" className="text-sm font-semibold text-teal-600 dark:text-teal-300">View flow</Link>
          </div>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {quick.map((l) => (
              <Link key={l.href} href={l.href} className="group rounded-2xl border border-[var(--border)] bg-[var(--card-soft)] p-4 transition hover:-translate-y-0.5 hover:border-teal-500/40 hover:shadow-xl hover:shadow-teal-950/5">
                <div className="text-sm font-bold">{l.label}</div>
                <div className="mt-2 flex items-center text-xs font-semibold text-teal-600 opacity-0 transition group-hover:opacity-100 dark:text-teal-300">
                  Open module <ArrowRight className="ml-1 h-3 w-3" />
                </div>
              </Link>
            ))}
          </div>
        </section>

        <section className="card">
          <div className="mb-5 flex items-center justify-between">
            <div>
              <h2 className="text-xl font-black tracking-tight">Recent projects</h2>
              <p className="text-sm text-[var(--muted)]">Continue where you left off.</p>
            </div>
            <Link href="/projects" className="text-sm font-semibold text-teal-600 dark:text-teal-300">All</Link>
          </div>
          {isLoading ? (
            <div className="rounded-2xl border border-dashed border-[var(--border)] p-6 text-sm text-[var(--muted)]">Loading projects...</div>
          ) : data.length === 0 ? (
            <div className="rounded-2xl border border-dashed border-[var(--border)] p-6">
              <p className="text-sm text-[var(--muted)]">No projects yet. Start with the Brand Wizard.</p>
              <Link className="btn mt-4" href="/wizard">Create first brand</Link>
            </div>
          ) : (
            <ul className="space-y-3">
              {data.slice(0, 6).map((p) => (
                <li key={p.id} className="rounded-2xl border border-[var(--border)] bg-[var(--card-soft)] p-4">
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <div className="font-bold">{p.name}</div>
                      <div className="mt-1 text-xs text-[var(--muted)]">{p.businessName || "Brand setup"} - {p.status}</div>
                    </div>
                    <Link className="btn btn-ghost min-h-0 px-4 py-2 text-xs" href={p.brandId ? `/projects/${p.id}` : "/wizard"}>Open</Link>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  );
}
