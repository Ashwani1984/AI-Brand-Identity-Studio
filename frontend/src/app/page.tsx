"use client";

import Link from "next/link";
import { motion } from "framer-motion";
import { ArrowRight, BadgeCheck, Layers3, Sparkles, Zap } from "lucide-react";

const features = [
  { icon: Sparkles, title: "AI brand systems", body: "Generate brand names, voice, palettes, typography, prompts, and guidelines in one guided flow." },
  { icon: Layers3, title: "Client-ready exports", body: "Package logos, guidelines, landing pages, and assets into professional Standard or Pro ZIP kits." },
  { icon: Zap, title: "Minutes, not weeks", body: "Turn business inputs into usable visual identity assets without design bottlenecks." },
];

export default function HomePage() {
  return (
    <main className="min-h-screen overflow-hidden bg-[#050914] text-white">
      <section className="relative min-h-screen px-6 py-8">
        <div className="absolute inset-0 surface-grid opacity-[0.18]" />
        <div className="absolute left-1/2 top-[-16rem] h-[38rem] w-[38rem] -translate-x-1/2 rounded-full bg-teal-400/20 blur-3xl" />
        <div className="absolute bottom-[-10rem] right-[-6rem] h-[30rem] w-[30rem] rounded-full bg-orange-500/10 blur-3xl" />

        <nav className="relative z-10 mx-auto flex max-w-7xl items-center justify-between rounded-full border border-white/10 bg-white/[0.05] px-4 py-3 backdrop-blur-xl">
          <div className="flex items-center gap-3">
            <div className="grid h-10 w-10 place-items-center rounded-full bg-teal-400/15 ring-1 ring-teal-300/25">
              <Sparkles className="h-5 w-5 text-teal-300" />
            </div>
            <div>
              <div className="text-sm font-black tracking-tight">AI Brand Identity Studio</div>
              <div className="text-xs text-slate-400">Enterprise brand AI</div>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Link href="/login" className="hidden rounded-full px-4 py-2 text-sm font-semibold text-slate-300 hover:text-white sm:block">Sign in</Link>
            <Link href="/register" className="btn">Start free</Link>
          </div>
        </nav>

        <div className="relative z-10 mx-auto grid min-h-[calc(100vh-6rem)] max-w-7xl items-center gap-12 py-16 lg:grid-cols-[1.05fr_0.95fr]">
          <motion.div initial={{ opacity: 0, y: 18 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.55 }}>
            <div className="mb-5 inline-flex items-center gap-2 rounded-full border border-teal-300/20 bg-teal-300/10 px-4 py-2 text-sm text-teal-100">
              <BadgeCheck className="h-4 w-4" /> Production-ready brand identity platform
            </div>
            <h1 className="max-w-4xl text-5xl font-black leading-[0.94] tracking-[-0.065em] md:text-7xl">
              Build complete brand identities in minutes.
            </h1>
            <p className="mt-6 max-w-2xl text-lg leading-8 text-slate-300">
              Replace slow agency handoffs with an AI studio that generates brand strategy, logos, prompts, guidelines, mockups, websites, and export packs from one enterprise workflow.
            </p>
            <div className="mt-9 flex flex-wrap gap-3">
              <Link href="/register" className="btn">
                Start creating <ArrowRight className="h-4 w-4" />
              </Link>
              <Link href="/login" className="btn btn-ghost border-white/15 bg-white/[0.06] text-white">
                Admin login
              </Link>
            </div>
            <div className="mt-10 grid max-w-xl grid-cols-3 gap-3 text-center">
              {[
                ["20+", "Modules"],
                ["10x", "Faster launch"],
                ["Pro", "Export kits"],
              ].map(([value, label]) => (
                <div key={label} className="rounded-3xl border border-white/10 bg-white/[0.04] p-4 backdrop-blur">
                  <div className="text-2xl font-black">{value}</div>
                  <div className="mt-1 text-xs text-slate-400">{label}</div>
                </div>
              ))}
            </div>
          </motion.div>

          <motion.div initial={{ opacity: 0, scale: 0.96 }} animate={{ opacity: 1, scale: 1 }} transition={{ duration: 0.65, delay: 0.1 }} className="relative">
            <div className="absolute -inset-4 rounded-[2rem] bg-teal-400/10 blur-2xl" />
            <div className="relative overflow-hidden rounded-[2rem] border border-white/10 bg-[#0b1220]/90 p-4 shadow-2xl shadow-black/40">
              <div className="mb-4 flex items-center gap-2">
                <span className="h-3 w-3 rounded-full bg-red-400" />
                <span className="h-3 w-3 rounded-full bg-yellow-400" />
                <span className="h-3 w-3 rounded-full bg-green-400" />
                <span className="ml-auto text-xs text-slate-500">Brand generation pipeline</span>
              </div>
              <div className="grid gap-4">
                <div className="rounded-3xl border border-teal-300/20 bg-teal-300/10 p-5">
                  <div className="text-xs uppercase tracking-[0.2em] text-teal-200">Generated brand</div>
                  <div className="mt-3 text-3xl font-black tracking-tight">NovaCore Labs</div>
                  <div className="mt-2 text-sm text-slate-300">Build from the core.</div>
                </div>
                <div className="grid grid-cols-5 gap-3">
                  {["#0F766E", "#0B1220", "#334155", "#F7F8FA", "#C2410C"].map((c) => (
                    <div key={c} className="rounded-2xl border border-white/10 p-2">
                      <div className="h-14 rounded-xl" style={{ background: c }} />
                      <div className="mt-2 text-[0.65rem] text-slate-400">{c}</div>
                    </div>
                  ))}
                </div>
                <div className="grid gap-3 sm:grid-cols-3">
                  {features.map((f) => (
                    <div key={f.title} className="rounded-3xl border border-white/10 bg-white/[0.04] p-4">
                      <f.icon className="h-5 w-5 text-teal-300" />
                      <div className="mt-3 text-sm font-bold">{f.title}</div>
                      <div className="mt-1 text-xs leading-5 text-slate-400">{f.body}</div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </motion.div>
        </div>
      </section>
    </main>
  );
}
