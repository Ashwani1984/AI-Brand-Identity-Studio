"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { login } from "@/lib/api";
import { useState } from "react";
import { ArrowRight, ShieldCheck, Sparkles } from "lucide-react";
import { Alert, Button, Field, Input } from "@/components/ui";

const schema = z.object({
  email: z.string().email(),
  password: z.string().min(8),
});

type Form = z.infer<typeof schema>;

export function LoginForm() {
  const router = useRouter();
  const search = useSearchParams();
  const [error, setError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { isSubmitting, errors },
  } = useForm<Form>({ resolver: zodResolver(schema) });

  const onSubmit = handleSubmit(async (values) => {
    try {
      setError(null);
      await login(values.email, values.password);
      router.push(search.get("next") || "/dashboard");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Login failed");
    }
  });

  return (
    <main className="grid min-h-screen bg-[#050914] text-white lg:grid-cols-[1fr_0.85fr]">
      <section className="relative hidden overflow-hidden p-10 lg:flex lg:flex-col lg:justify-between">
        <div className="absolute inset-0 surface-grid opacity-10" />
        <div className="absolute -left-24 top-20 h-80 w-80 rounded-full bg-teal-400/20 blur-3xl" />
        <div className="relative z-10 flex items-center gap-3">
          <div className="grid h-11 w-11 place-items-center rounded-2xl bg-teal-400/15 ring-1 ring-teal-300/25">
            <Sparkles className="h-5 w-5 text-teal-300" />
          </div>
          <div className="font-black tracking-tight">AI Brand Identity Studio</div>
        </div>
        <div className="relative z-10 max-w-xl">
          <p className="kicker text-teal-300">Enterprise login</p>
          <h1 className="mt-4 text-5xl font-black leading-[0.95] tracking-[-0.06em]">
            Create, package, and sell brand systems faster.
          </h1>
          <p className="mt-5 text-slate-300">
            Secure workspace access for brand generation, exports, payments, and admin analytics.
          </p>
        </div>
        <div className="relative z-10 rounded-3xl border border-white/10 bg-white/[0.04] p-5 text-sm text-slate-300">
          <ShieldCheck className="mb-3 h-5 w-5 text-teal-300" />
          Admin demo: <span className="font-semibold text-white">admin@aibrand.studio</span> /{" "}
          <span className="font-semibold text-white">Admin@12345</span>
        </div>
      </section>

      <section className="flex items-center justify-center px-5 py-10">
        <form
          onSubmit={onSubmit}
          className="w-full max-w-md rounded-[2rem] border border-white/10 bg-white/[0.06] p-6 shadow-2xl shadow-black/30 backdrop-blur-xl md:p-8"
        >
          <div className="mb-8">
            <div className="mb-4 inline-flex rounded-full border border-teal-300/20 bg-teal-300/10 px-3 py-1 text-xs font-bold uppercase tracking-wider text-teal-100">
              Welcome back
            </div>
            <h1 className="text-3xl font-black tracking-tight">Sign in</h1>
            <p className="mt-2 text-sm text-slate-400">Access your AI brand studio workspace.</p>
          </div>
          <div className="space-y-4">
            <Field label="Email" error={errors.email?.message}>
              <Input className="bg-white text-slate-950" placeholder="Email" type="email" {...register("email")} />
            </Field>
            <Field label="Password" error={errors.password?.message}>
              <Input className="bg-white text-slate-950" placeholder="Password" type="password" {...register("password")} />
            </Field>
            {error && <Alert tone="error">{error}</Alert>}
            <Button type="submit" className="w-full" disabled={isSubmitting}>
              {isSubmitting ? "Signing in..." : "Sign in"} <ArrowRight className="h-4 w-4" />
            </Button>
          </div>
          <p className="mt-6 text-center text-sm text-slate-400">
            No account?{" "}
            <Link className="font-semibold text-teal-300" href="/register">
              Create one
            </Link>
          </p>
        </form>
      </section>
    </main>
  );
}
