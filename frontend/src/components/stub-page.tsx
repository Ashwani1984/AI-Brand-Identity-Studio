export default function StubPage({ title, note }: { title: string; note: string }) {
  return (
    <div className="card">
      <h2 className="text-2xl font-semibold">{title}</h2>
      <p className="mt-2 text-[var(--muted)]">{note}</p>
      <p className="mt-4 text-sm">Phase 2 module scaffold - API interfaces reserved.</p>
    </div>
  );
}
