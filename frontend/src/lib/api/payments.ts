import { api } from "./client";

export type Plan = {
  id: string;
  code: string;
  name: string;
  monthlyPriceInr: number;
  monthlyCredits: number;
};

export type RazorpayOrder = {
  orderId: string;
  amount: number;
  currency: string;
  razorpayKeyId: string;
  paymentId: string;
  planCode: string;
};

export function listPlans() {
  return api<Plan[]>("/api/subscriptions/plans");
}

export function createRazorpayOrder(planCode: string) {
  return api<RazorpayOrder>("/api/payments/razorpay/order", {
    method: "POST",
    body: JSON.stringify({ planCode }),
  });
}
