"use client";

import Link from "next/link";
import { cn } from "@/lib/cn";

type AlertProps = {
  tone?: "error" | "info" | "success";
  title?: string;
  children: React.ReactNode;
  actionHref?: string;
  actionLabel?: string;
  className?: string;
};

const tones = {
  error: "border-red-400/30 bg-red-500/10 text-red-800 dark:text-red-100",
  info: "border-teal-400/30 bg-teal-500/10 text-teal-900 dark:text-teal-100",
  success: "border-emerald-400/30 bg-emerald-500/10 text-emerald-900 dark:text-emerald-100",
};

export function Alert({ tone = "info", title, children, actionHref, actionLabel, className }: AlertProps) {
  return (
    <div className={cn("rounded-2xl border p-4 text-sm", tones[tone], className)} role="alert">
      {title && <div className="mb-1 font-bold">{title}</div>}
      <div>{children}</div>
      {actionHref && actionLabel && (
        <Link href={actionHref} className="mt-3 inline-flex font-semibold underline">
          {actionLabel}
        </Link>
      )}
    </div>
  );
}
