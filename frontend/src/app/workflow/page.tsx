"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";

const steps = [
  { n: 1, title: "Register / Login", href: "/register", desc: "Create account - JWT session" },
  { n: 2, title: "Dashboard", href: "/dashboard", desc: "Credits, stats, quick launch" },
  { n: 3, title: "Create Project / Brand", href: "/wizard", desc: "Brand Wizard business inputs" },
  { n: 4, title: "Generate Brand", href: "/wizard", desc: "Name, tagline, story, voice" },
  { n: 5, title: "Generate Logo", href: "/logo", desc: "SVG/PNG - styles - regenerate" },
  { n: 6, title: "Generate Mockups", href: "/mockups", desc: "Cards, cups, packaging, apparel" },
  { n: 7, title: "Generate Website", href: "/landing", desc: "Responsive HTML landing" },
  { n: 8, title: "Guidelines + Export", href: "/downloads", desc: "Brand book - Standard/Pro ZIP" },
  { n: 9, title: "Payments", href: "/billing", desc: "Upgrade credits - Razorpay" },
];

export default function WorkflowPage() {
  const projects = useQuery({
    queryKey: ["projects"],
    queryFn: () => api<Array<{ id: string; brandId?: string | null }>>("/api/projects"),
  });
  const hasBrand = (projects.data || []).some((p) => p.brandId);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-semibold tracking-tight">Enterprise user flow</h2>
        <p className="text-[var(--muted)]">
          Register → Login → Dashboard → Create Project → Generate Brand → Logo → Mockups → Website → Export
        </p>
      </div>
      <div className="card text-sm">
        Status: {hasBrand ? "Brand project detected - continue from Logo or Export." : "No brand yet - start at Brand Wizard."}
      </div>
      <ol className="space-y-3">
        {steps.map((s) => (
          <li key={s.n} className="card flex flex-wrap items-center justify-between gap-3">
            <div>
              <div className="text-xs uppercase tracking-wider text-teal-500">Step {s.n}</div>
              <div className="text-lg font-semibold">{s.title}</div>
              <p className="text-sm text-[var(--muted)]">{s.desc}</p>
            </div>
            <Link href={s.href} className="btn">
              Open
            </Link>
          </li>
        ))}
      </ol>
    </div>
  );
}
