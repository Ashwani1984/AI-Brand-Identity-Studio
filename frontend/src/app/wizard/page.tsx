"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { api } from "@/lib/api";
import { motion, AnimatePresence } from "framer-motion";

const schema = z.object({
  projectName: z.string().optional(),
  businessName: z.string().min(2),
  industry: z.string().optional(),
  audience: z.string().optional(),
  competitors: z.string().optional(),
  country: z.string().optional(),
  language: z.string().optional(),
  style: z.string().optional(),
  mood: z.string().optional(),
  preferredColors: z.string().optional(),
  preferredFonts: z.string().optional(),
  keywords: z.string().optional(),
  mission: z.string().optional(),
  vision: z.string().optional(),
  usp: z.string().optional(),
});

type Form = z.infer<typeof schema>;

const steps = [
  ["businessName", "industry", "audience", "competitors"],
  ["country", "language", "style", "mood"],
  ["preferredColors", "preferredFonts", "keywords"],
  ["mission", "vision", "usp", "projectName"],
] as const;

export default function WizardPage() {
  const router = useRouter();
  const [step, setStep] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const form = useForm<Form>({ resolver: zodResolver(schema), defaultValues: { style: "Minimal", mood: "Premium" } });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      setError(null);
      const brand = await api<{ id: string; projectId: string }>("/api/brands/wizard", {
        method: "POST",
        body: JSON.stringify(values),
      });
      await api(`/api/brands/${brand.id}/generate`, { method: "POST" });
      router.push(`/projects/${brand.projectId}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Wizard failed");
    }
  });

  const fields = steps[step];

  const nextStep = async () => {
    if (step === 0) {
      const ok = await form.trigger("businessName");
      if (!ok) {
        setError("Business name is required (min 2 characters).");
        return;
      }
    }
    setError(null);
    setStep((s) => s + 1);
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <h2 className="text-3xl font-semibold">Brand Wizard</h2>
        <p className="text-[var(--muted)]">Step {step + 1} of {steps.length} - enter business info, then AI generates the brand kit.</p>
      </div>
      <div className="h-2 overflow-hidden rounded-full bg-white/10">
        <div className="h-full bg-teal-600 transition-all" style={{ width: `${((step + 1) / steps.length) * 100}%` }} />
      </div>
      <form onSubmit={onSubmit} className="card space-y-4">
        <AnimatePresence mode="wait">
          <motion.div
            key={step}
            initial={{ opacity: 0, x: 12 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: -12 }}
            className="space-y-3"
          >
            {fields.map((name) => (
              <div key={name}>
                <label className="mb-1 block text-sm capitalize text-[var(--muted)]">
                  {name.replace(/([A-Z])/g, " $1")}
                  {name === "businessName" ? " *" : ""}
                </label>
                <input className="input" {...form.register(name)} />
              </div>
            ))}
          </motion.div>
        </AnimatePresence>
        {error && <p className="text-sm text-red-500">{error}</p>}
        <div className="flex justify-between gap-3 pt-2">
          <button type="button" className="btn btn-ghost" disabled={step === 0} onClick={() => setStep((s) => s - 1)}>
            Back
          </button>
          {step < steps.length - 1 ? (
            <button type="button" className="btn" onClick={nextStep}>
              Continue
            </button>
          ) : (
            <button type="submit" className="btn" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting ? "Generating..." : "Generate brand"}
            </button>
          )}
        </div>
      </form>
    </div>
  );
}
