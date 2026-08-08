"use client";

import Link from "next/link";
import { Alert, Button } from "@/components/ui";

export function SegmentError({
  error,
  reset,
  title = "Something went wrong",
}: {
  error: Error & { digest?: string };
  reset: () => void;
  title?: string;
}) {
  return (
    <div className="mx-auto max-w-xl space-y-4 py-16">
      <Alert tone="error" title={title} actionHref="/billing" actionLabel="Check billing / credits">
        {error.message || "An unexpected error occurred in this workspace section."}
        {error.digest ? ` (ref ${error.digest})` : null}
      </Alert>
      <div className="flex flex-wrap gap-3">
        <Button onClick={reset}>Try again</Button>
        <Link href="/dashboard" className="btn-ghost inline-flex items-center rounded-2xl px-4 py-2.5 text-sm font-semibold">
          Back to dashboard
        </Link>
      </div>
    </div>
  );
}
