"use client";

import Link from "next/link";
import { getStoredUser } from "@/lib/api";
import { ArrowRight, Crown, TrendingUp } from "lucide-react";

export function UpgradeBanner({
  title = "Unlock premium revenue assets",
  body = "Upgrade for higher credit limits, Pro ZIP exports, multi-tool prompt packs, and client-ready brand guidelines.",
}: {
  title?: string;
  body?: string;
}) {
  const credits = getStoredUser()?.credits ?? 0;
  if (credits > 20) return null;
  return (
    <div className="relative overflow-hidden rounded-[1.5rem] border border-teal-500/25 bg-[#07111f] p-5 text-white shadow-2xl shadow-teal-950/10">
      <div className="absolute -right-12 -top-12 h-32 w-32 rounded-full bg-teal-400/20 blur-2xl" />
      <div className="relative flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-start gap-4">
          <div className="grid h-11 w-11 place-items-center rounded-2xl bg-teal-300/10 text-teal-200 ring-1 ring-teal-300/20"><Crown className="h-5 w-5" /></div>
          <div>
            <div className="font-black tracking-tight">{title}</div>
            <p className="mt-1 max-w-2xl text-sm leading-6 text-slate-300">{body}</p>
            <div className="mt-2 inline-flex items-center gap-2 rounded-full bg-amber-400/10 px-3 py-1 text-xs font-semibold text-amber-200">
              <TrendingUp className="h-3.5 w-3.5" /> {credits} credits left
            </div>
          </div>
        </div>
        <Link href="/billing" className="btn shrink-0">
          Upgrade plan <ArrowRight className="h-4 w-4" />
        </Link>
      </div>
    </div>
  );
}

export function RevenueHint({ children }: { children: React.ReactNode }) {
  return <p className="text-sm leading-6 text-[var(--muted)]">{children}</p>;
}

export function InsufficientCreditsHint({ message }: { message?: string }) {
  if (!message?.toLowerCase().includes("credit")) return null;
  return (
    <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm">
      {message}{" "}
      <Link className="font-bold text-teal-600 underline dark:text-teal-300" href="/billing">Buy credits / upgrade</Link>
    </div>
  );
}
