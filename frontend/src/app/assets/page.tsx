"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";

type Asset = { id: string; kind: string; fileName: string; url: string; byteSize: number; createdAtUtc: string };

export default function AssetsPage() {
  const { data = [], isLoading } = useQuery({
    queryKey: ["assets"],
    queryFn: () => api<Asset[]>("/api/assets"),
  });

  return (
    <div className="space-y-6">
      <h2 className="text-3xl font-semibold">Asset Library</h2>
      <div className="card overflow-x-auto">
        {isLoading ? (
          <p>Loading...</p>
        ) : data.length === 0 ? (
          <p className="text-[var(--muted)]">No assets yet. Generate logos or export a ZIP.</p>
        ) : (
          <table className="w-full text-left text-sm">
            <thead className="text-[var(--muted)]">
              <tr>
                <th className="pb-2">Kind</th>
                <th className="pb-2">File</th>
                <th className="pb-2">Size</th>
                <th className="pb-2">Open</th>
              </tr>
            </thead>
            <tbody>
              {data.map((a) => (
                <tr key={a.id} className="border-t border-white/5">
                  <td className="py-3">{a.kind}</td>
                  <td>{a.fileName}</td>
                  <td>{Math.round(a.byteSize / 1024)} KB</td>
                  <td>
                    <a className="text-teal-500" href={`${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5080"}${a.url}`} target="_blank">
                      Download
                    </a>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
