export default function Loading() {
  return (
    <div className="space-y-4 py-8" aria-busy="true" aria-label="Loading">
      <div className="h-8 w-48 animate-pulse rounded-xl bg-[var(--border)]" />
      <div className="h-40 animate-pulse rounded-[1.5rem] bg-[var(--card)]" />
      <div className="grid gap-4 md:grid-cols-2">
        <div className="h-32 animate-pulse rounded-[1.5rem] bg-[var(--card)]" />
        <div className="h-32 animate-pulse rounded-[1.5rem] bg-[var(--card)]" />
      </div>
    </div>
  );
}
