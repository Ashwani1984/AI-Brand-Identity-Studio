"use client";

import { Suspense } from "react";
import { LoginForm } from "@/features/auth/login-form";

export default function LoginPage() {
  return (
    <Suspense fallback={<div className="grid min-h-screen place-items-center bg-[#050914] text-white">Loading…</div>}>
      <LoginForm />
    </Suspense>
  );
}
