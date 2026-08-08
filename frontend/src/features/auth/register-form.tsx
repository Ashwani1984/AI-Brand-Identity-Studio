"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { register as registerUser } from "@/lib/api";
import { useState } from "react";
import { ArrowRight, CheckCircle2, Sparkles } from "lucide-react";
import { Alert, Button, Field, Input } from "@/components/ui";

const schema = z.object({
  fullName: z.string().min(2),
  email: z.string().email(),
  password: z.string().min(8),
});

type Form = z.infer<typeof schema>;

export function RegisterForm() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { isSubmitting, errors },
  } = useForm<Form>({ resolver: zodResolver(schema) });

  const onSubmit = handleSubmit(async (values) => {
    try {
      setError(null);
      await registerUser(values.fullName, values.email, values.password);
      router.push("/dashboard");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Registration failed");
    }
  });

  return (
    <main className="grid min-h-screen bg-[#050914] text-white lg:grid-cols-[0.9fr_1fr]">
      <section className="flex items-center justify-center px-5 py-10">
        <form
          onSubmit={onSubmit}
          className="w-full max-w-md rounded-[2rem] border border-white/10 bg-white/[0.06] p-6 shadow-2xl shadow-black/30 backdrop-blur-xl md:p-8"
        >
          <div className="mb-8">
            <div className="mb-4 inline-flex rounded-full border border-teal-300/20 bg-teal-300/10 px-3 py-1 text-xs font-bold uppercase tracking-wider text-teal-100">
              Start free
            </div>
            <h1 className="text-3xl font-black tracking-tight">Create account</h1>
            <p className="mt-2 text-sm text-slate-400">Launch your first brand kit in minutes.</p>
          </div>
          <div className="space-y-4">
            <Field label="Full name" error={errors.fullName?.message}>
              <Input className="bg-white text-slate-950" placeholder="Full name" {...register("fullName")} />
            </Field>
            <Field label="Email" error={errors.email?.message}>
              <Input className="bg-white text-slate-950" placeholder="Email" type="email" {...register("email")} />
            </Field>
            <Field label="Password" error={errors.password?.message}>
              <Input className="bg-white text-slate-950" placeholder="Password" type="password" {...register("password")} />
            </Field>
            {error && <Alert tone="error">{error}</Alert>}
            <Button type="submit" className="w-full" disabled={isSubmitting}>
              {isSubmitting ? "Creating..." : "Create account"} <ArrowRight className="h-4 w-4" />
            </Button>
          </div>
          <p className="mt-6 text-center text-sm text-slate-400">
            Have an account?{" "}
            <Link className="font-semibold text-teal-300" href="/login">
              Sign in
            </Link>
          </p>
        </form>
      </section>
      <section className="relative hidden overflow-hidden p-10 lg:flex lg:flex-col lg:justify-center">
        <div className="absolute inset-0 surface-grid opacity-10" />
        <div className="relative z-10 max-w-lg">
          <Sparkles className="mb-4 h-8 w-8 text-teal-300" />
          <h2 className="text-4xl font-black tracking-tight">Everything you need to ship identity systems.</h2>
          <ul className="mt-6 space-y-3 text-slate-300">
            {["AI brand + logo packs", "Client-ready guidelines", "Credits & Razorpay billing"].map((item) => (
              <li key={item} className="flex items-center gap-2">
                <CheckCircle2 className="h-4 w-4 text-teal-300" /> {item}
              </li>
            ))}
          </ul>
        </div>
      </section>
    </main>
  );
}
