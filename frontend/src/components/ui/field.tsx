"use client";

import { InputHTMLAttributes, TextareaHTMLAttributes, forwardRef } from "react";
import { cn } from "@/lib/cn";

type FieldProps = {
  label?: string;
  hint?: string;
  error?: string;
  className?: string;
  children: React.ReactNode;
};

export function Field({ label, hint, error, className, children }: FieldProps) {
  return (
    <label className={cn("block space-y-2", className)}>
      {label && <span className="block text-sm font-semibold text-[var(--muted)]">{label}</span>}
      {children}
      {hint && !error && <span className="block text-xs text-[var(--muted)]">{hint}</span>}
      {error && <span className="block text-xs font-medium text-red-600 dark:text-red-300">{error}</span>}
    </label>
  );
}

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  ({ className, ...props }, ref) => <input ref={ref} className={cn("input", className)} {...props} />,
);
Input.displayName = "Input";

export const TextArea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(
  ({ className, ...props }, ref) => (
    <textarea ref={ref} className={cn("input min-h-28", className)} {...props} />
  ),
);
TextArea.displayName = "TextArea";

export const Select = forwardRef<HTMLSelectElement, React.SelectHTMLAttributes<HTMLSelectElement>>(
  ({ className, children, ...props }, ref) => (
    <select ref={ref} className={cn("input", className)} {...props}>
      {children}
    </select>
  ),
);
Select.displayName = "Select";
