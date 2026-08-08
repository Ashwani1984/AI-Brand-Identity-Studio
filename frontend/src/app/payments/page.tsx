"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { createRazorpayOrder, listPlans, type Plan } from "@/lib/api";
import { useState } from "react";
import { Alert, Button } from "@/components/ui";

export default function PaymentsPage() {
  const [msg, setMsg] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const { data: plans = [] } = useQuery({
    queryKey: ["plans"],
    queryFn: listPlans,
  });
  const order = useMutation({
    mutationFn: (planCode: string) => createRazorpayOrder(planCode),
    onSuccess: (d) => {
      setError(null);
      setMsg(`Razorpay order ${d.orderId} created for plan ${d.planCode} (₹${(d.amount / 100).toFixed(0)})`);
    },
    onError: (e: Error) => {
      setMsg(null);
      setError(e.message);
    },
  });

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-semibold">Payments</h2>
        <p className="text-[var(--muted)]">Razorpay checkout (order by plan; webhook grants credits on capture).</p>
      </div>
      {msg && <Alert tone="success">{msg}</Alert>}
      {error && <Alert tone="error">{error}</Alert>}
      <div className="grid gap-4 md:grid-cols-3">
        {plans.filter((p: Plan) => p.monthlyPriceInr > 0).map((p) => (
          <div key={p.id} className="card space-y-3">
            <h3 className="text-xl font-semibold">{p.name}</h3>
            <p className="text-3xl font-semibold">₹{p.monthlyPriceInr}</p>
            <Button className="w-full" onClick={() => order.mutate(p.code)} disabled={order.isPending}>
              Pay with Razorpay
            </Button>
          </div>
        ))}
      </div>
    </div>
  );
}
