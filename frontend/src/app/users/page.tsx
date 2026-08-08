"use client";

import { useQuery } from "@tanstack/react-query";
import { api, getStoredUser } from "@/lib/api";

type User = { id: string; email: string; fullName: string; creditBalance: number; isActive: boolean };

export default function UsersPage() {
  const isAdmin = getStoredUser()?.roles?.includes("Admin");
  const { data = [], isLoading, error } = useQuery({
    queryKey: ["admin-users"],
    enabled: !!isAdmin,
    queryFn: () => api<User[]>("/api/admin/users"),
    retry: false,
  });

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-semibold">User Management</h2>
        <p className="text-[var(--muted)]">View accounts and credit balances (Admin only).</p>
      </div>
      {!isAdmin ? (
        <div className="card text-sm text-[var(--muted)]">
          Admin role required. Use <code>admin@aibrand.studio</code> / <code>Admin@12345</code>
        </div>
      ) : (
        <div className="card overflow-x-auto">
          {isLoading && <p>Loading...</p>}
          {error && <p className="text-red-500">{(error as Error).message}</p>}
          <table className="w-full text-left text-sm">
            <thead className="text-[var(--muted)]">
              <tr>
                <th className="pb-2">Name</th>
                <th className="pb-2">Email</th>
                <th className="pb-2">Credits</th>
                <th className="pb-2">Active</th>
              </tr>
            </thead>
            <tbody>
              {data.map((u) => (
                <tr key={u.id} className="border-t border-white/5">
                  <td className="py-3">{u.fullName}</td>
                  <td>{u.email}</td>
                  <td>{u.creditBalance}</td>
                  <td>{u.isActive ? "Yes" : "No"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
