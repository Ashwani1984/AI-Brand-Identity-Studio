"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { createRazorpayOrder, getStoredUser, listPlans, type Plan } from "@/lib/api";
import { useState } from "react";
import Link from "next/link";
import { Alert, Button } from "@/components/ui";

const features: Record<string, string[]> = {
  free: ["50 credits / month", "Standard ZIP exports", "Core generators", "Community support"],
  builder: ["300 credits / month", "Pro ZIP exports", "Priority generation", "Client prompt packs", "Email support"],
  scale: ["1200 credits / month", "Unlimited Pro kits*", "White-label guidelines", "Team seats (coming)", "Priority support"],
};

export default function BillingPage() {
  const [msg, setMsg] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const credits = getStoredUser()?.credits ?? 0;
  const { data: plans = [] } = useQuery({
    queryKey: ["plans"],
    queryFn: listPlans,
  });

  const order = useMutation({
    mutationFn: (planCode: string) => createRazorpayOrder(planCode),
    onSuccess: (data) => {
      setError(null);
      setMsg(
        `Razorpay order created: ${data.orderId} for ${data.planCode} (₹${(data.amount / 100).toFixed(0)}). Complete payment to unlock credits.`,
      );
    },
    onError: (e: Error) => {
      setMsg(null);
      setError(e.message);
    },
  });

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-semibold tracking-tight">Subscription</h2>
        <p className="text-[var(--muted)]">
          You have <strong>{credits}</strong> credits. Upgrade to sell more brand kits and keep clients on retainer.
        </p>
      </div>
      {msg && <Alert tone="success">{msg}</Alert>}
      {error && <Alert tone="error">{error}</Alert>}
      <div className="grid gap-4 lg:grid-cols-3">
        {plans.map((p: Plan) => {
          const popular = p.code === "builder";
          return (
            <div key={p.id} className={`card relative space-y-4 ${popular ? "border-teal-500/50 ring-1 ring-teal-500/30" : ""}`}>
              {popular && (
                <div className="absolute -top-3 right-4 rounded-full bg-teal-600 px-3 py-0.5 text-xs font-semibold text-white">
                  Most popular
                </div>
              )}
              <div>
                <h3 className="text-xl font-semibold">{p.name}</h3>
                <p className="mt-2 text-3xl font-semibold">
                  ₹{p.monthlyPriceInr}
                  <span className="text-sm font-normal text-[var(--muted)]">/mo</span>
                </p>
                <p className="mt-1 text-sm text-[var(--muted)]">{p.monthlyCredits} credits / month</p>
              </div>
              <ul className="space-y-2 text-sm text-[var(--muted)]">
                {(features[p.code] || []).map((f) => (
                  <li key={f}>• {f}</li>
                ))}
              </ul>
              <Button
                className="w-full"
                disabled={p.monthlyPriceInr === 0 || order.isPending}
                onClick={() => order.mutate(p.code)}
              >
                {p.monthlyPriceInr === 0 ? "Current free tier" : "Checkout with Razorpay"}
              </Button>
            </div>
          );
        })}
      </div>
      <div className="card text-sm text-[var(--muted)]">
        Revenue tip: Bundle <Link className="text-teal-400" href="/guidelines">Brand Guidelines</Link> +{" "}
        <Link className="text-teal-400" href="/downloads">Pro ZIP</Link> +{" "}
        <Link className="text-teal-400" href="/prompts">Prompt packs</Link> as a monthly client retainer.
      </div>
    </div>
  );
}
