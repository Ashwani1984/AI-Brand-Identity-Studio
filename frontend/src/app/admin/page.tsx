"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { api, getStoredUser } from "@/lib/api";

type AdminUser = { id: string; email: string; fullName: string; creditBalance: number; isActive: boolean };
type AuditRow = { id: string; userId?: string; action: string; entityType?: string; createdAtUtc: string };
type LogsPayload = {
  totalUsers: number;
  recentPrompts: Array<{ kind: string; provider: string; createdAtUtc: string }>;
  recentAudit?: AuditRow[];
};

export default function AdminPanelPage() {
  const isAdmin = getStoredUser()?.roles?.includes("Admin");
  const qc = useQueryClient();
  const [grantUserId, setGrantUserId] = useState("");
  const [grantAmount, setGrantAmount] = useState("50");
  const [grantReason, setGrantReason] = useState("Support goodwill");
  const [reportYear, setReportYear] = useState(new Date().getFullYear());
  const [reportMonth, setReportMonth] = useState(new Date().getMonth() + 1);
  const [message, setMessage] = useState<string | null>(null);

  const users = useQuery({
    queryKey: ["admin-users"],
    enabled: !!isAdmin,
    queryFn: () => api<AdminUser[]>("/api/admin/users"),
    retry: false,
  });
  const logs = useQuery({
    queryKey: ["admin-logs"],
    enabled: !!isAdmin,
    queryFn: () => api<LogsPayload>("/api/admin/logs"),
    retry: false,
  });
  const audit = useQuery({
    queryKey: ["admin-audit"],
    enabled: !!isAdmin,
    queryFn: () => api<AuditRow[]>("/api/admin/audit?take=40"),
    retry: false,
  });

  const grant = useMutation({
    mutationFn: () =>
      api<{ creditBalance: number }>("/api/admin/credits/grant", {
        method: "POST",
        body: JSON.stringify({
          userId: grantUserId,
          amount: Number(grantAmount),
          reason: grantReason,
        }),
      }),
    onSuccess: (data) => {
      setMessage(`Granted. Balance now ${data.creditBalance}.`);
      qc.invalidateQueries({ queryKey: ["admin-users"] });
      qc.invalidateQueries({ queryKey: ["admin-audit"] });
      qc.invalidateQueries({ queryKey: ["admin-logs"] });
    },
    onError: (e: Error) => setMessage(e.message),
  });

  async function downloadReport() {
    setMessage(null);
    try {
      const token = typeof window !== "undefined" ? localStorage.getItem("abs_access") : null;
      const base = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5080";
      const res = await fetch(`${base}/api/admin/reports/monthly?year=${reportYear}&month=${reportMonth}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });
      if (!res.ok) throw new Error(await res.text());
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `admin-report-${reportYear}-${String(reportMonth).padStart(2, "0")}.csv`;
      a.click();
      URL.revokeObjectURL(url);
      setMessage("Report downloaded.");
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Report failed");
    }
  }

  if (!isAdmin) {
    return (
      <div className="card">
        <h2 className="text-2xl font-semibold">Admin Panel</h2>
        <p className="mt-2 text-[var(--muted)]">Sign in as admin@aibrand.studio / Admin@12345</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <h2 className="text-3xl font-semibold">Admin Panel</h2>
      {message && <p className="text-sm text-[var(--muted)]">{message}</p>}

      <div className="grid gap-4 md:grid-cols-3">
        <div className="card">
          <div className="text-sm text-[var(--muted)]">Users</div>
          <div className="text-3xl font-semibold">{logs.data?.totalUsers ?? users.data?.length ?? 0}</div>
        </div>
        <div className="card">
          <div className="text-sm text-[var(--muted)]">Recent AI jobs</div>
          <div className="text-3xl font-semibold">{logs.data?.recentPrompts?.length ?? 0}</div>
        </div>
        <div className="card">
          <div className="text-sm text-[var(--muted)]">Audit events</div>
          <div className="text-3xl font-semibold">{audit.data?.length ?? 0}</div>
        </div>
      </div>

      <div className="card space-y-3">
        <h3 className="font-semibold">Grant / revoke credits</h3>
        <div className="grid gap-2 md:grid-cols-4">
          <select
            className="rounded border border-white/10 bg-transparent px-3 py-2 text-sm"
            value={grantUserId}
            onChange={(e) => setGrantUserId(e.target.value)}
          >
            <option value="">Select user</option>
            {(users.data || []).map((u) => (
              <option key={u.id} value={u.id}>
                {u.email} ({u.creditBalance})
              </option>
            ))}
          </select>
          <input
            className="rounded border border-white/10 bg-transparent px-3 py-2 text-sm"
            value={grantAmount}
            onChange={(e) => setGrantAmount(e.target.value)}
            placeholder="Amount (+/-)"
          />
          <input
            className="rounded border border-white/10 bg-transparent px-3 py-2 text-sm md:col-span-1"
            value={grantReason}
            onChange={(e) => setGrantReason(e.target.value)}
            placeholder="Reason"
          />
          <button
            type="button"
            className="rounded bg-[var(--accent)] px-3 py-2 text-sm font-medium text-black disabled:opacity-50"
            disabled={!grantUserId || grant.isPending}
            onClick={() => grant.mutate()}
          >
            Apply
          </button>
        </div>
      </div>

      <div className="card space-y-3">
        <h3 className="font-semibold">Monthly finance CSV</h3>
        <div className="flex flex-wrap items-end gap-2">
          <label className="text-sm">
            Year
            <input
              type="number"
              className="ml-2 w-24 rounded border border-white/10 bg-transparent px-2 py-1"
              value={reportYear}
              onChange={(e) => setReportYear(Number(e.target.value))}
            />
          </label>
          <label className="text-sm">
            Month
            <input
              type="number"
              min={1}
              max={12}
              className="ml-2 w-16 rounded border border-white/10 bg-transparent px-2 py-1"
              value={reportMonth}
              onChange={(e) => setReportMonth(Number(e.target.value))}
            />
          </label>
          <button type="button" className="rounded border border-white/20 px-3 py-2 text-sm" onClick={downloadReport}>
            Download report
          </button>
        </div>
      </div>

      <div className="card overflow-x-auto">
        <h3 className="mb-3 font-semibold">Users</h3>
        <table className="w-full text-left text-sm">
          <thead className="text-[var(--muted)]">
            <tr>
              <th className="pb-2">Name</th>
              <th className="pb-2">Email</th>
              <th className="pb-2">Credits</th>
            </tr>
          </thead>
          <tbody>
            {(users.data || []).map((u) => (
              <tr key={u.id} className="border-t border-white/5">
                <td className="py-3">{u.fullName}</td>
                <td>{u.email}</td>
                <td>{u.creditBalance}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card">
        <h3 className="mb-3 font-semibold">Audit trail</h3>
        <ul className="space-y-2 text-sm">
          {(audit.data || logs.data?.recentAudit || []).map((a) => (
            <li key={a.id} className="flex justify-between border-b border-white/5 pb-2">
              <span>{a.action}{a.entityType ? ` · ${a.entityType}` : ""}</span>
              <span className="text-[var(--muted)]">{new Date(a.createdAtUtc).toLocaleString()}</span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
