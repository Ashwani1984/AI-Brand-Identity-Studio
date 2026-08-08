"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { AuthResponse, clearAuth, getStoredUser, refreshProfile } from "@/lib/api";
import { useEffect, useMemo, useState } from "react";
import { ChevronRight, Menu, Moon, Search, Sparkles, Sun, X } from "lucide-react";
import { useTheme } from "next-themes";
import { links } from "@/components/nav-links";

const groups = [
  { label: "Operate", items: ["/dashboard", "/workflow", "/projects"] },
  { label: "Create", items: ["/wizard", "/logo", "/prompts", "/palette", "/typography", "/mockups", "/landing", "/video"] },
  { label: "Grow", items: ["/marketing", "/social", "/guidelines", "/assets", "/downloads", "/billing", "/payments"] },
  { label: "Control", items: ["/users", "/admin", "/analytics"] },
];

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const { theme, setTheme } = useTheme();
  const [user, setUser] = useState<AuthResponse | null>(() => getStoredUser());
  const [open, setOpen] = useState(false);

  const current = useMemo(() => links.find((l) => pathname === l.href || (l.href !== "/dashboard" && pathname.startsWith(l.href))), [pathname]);

  const syncUser = () => {
    setUser(getStoredUser());
  };

  useEffect(() => {
    // Middleware gates routes; keep profile refresh for credits/roles.
    void refreshProfile().then(() => syncUser());
    const onAuth = () => syncUser();
    const onFocus = () => void refreshProfile().then(() => syncUser());
    window.addEventListener("abs-auth-changed", onAuth);
    window.addEventListener("focus", onFocus);
    return () => {
      window.removeEventListener("abs-auth-changed", onAuth);
      window.removeEventListener("focus", onFocus);
    };
  }, [pathname]);

  const isAuthPage = pathname === "/login" || pathname === "/register" || pathname === "/";
  if (isAuthPage) return <>{children}</>;

  const name = user?.fullName ?? "User";
  const credits = user?.credits ?? 0;

  const Nav = (
    <>
      <div className="mb-7 flex items-center gap-3">
        <div className="grid h-11 w-11 place-items-center rounded-2xl bg-teal-500/15 ring-1 ring-teal-400/20">
          <Sparkles className="h-5 w-5 text-teal-300" />
        </div>
        <div>
          <div className="text-base font-black tracking-tight text-white">AI Brand Studio</div>
          <div className="text-xs text-[var(--sidebar-muted)]">Enterprise identity AI</div>
        </div>
      </div>

      <div className="mb-5 flex items-center gap-2 rounded-2xl border border-white/10 bg-white/[0.04] px-3 py-2 text-xs text-slate-400">
        <Search className="h-3.5 w-3.5" />
        Search modules
      </div>

      <nav className="flex flex-1 flex-col gap-5 overflow-y-auto pr-1">
        {groups.map((group) => (
          <div key={group.label}>
            <div className="mb-2 px-2 text-[0.66rem] font-bold uppercase tracking-[0.18em] text-slate-500">{group.label}</div>
            <div className="space-y-1">
              {links.filter((l) => group.items.includes(l.href)).map((l) => {
                const active = pathname === l.href || (l.href !== "/dashboard" && pathname.startsWith(l.href));
                return (
                  <Link
                    key={l.href}
                    href={l.href}
                    onClick={() => setOpen(false)}
                    className={`group flex items-center justify-between rounded-2xl px-3 py-2.5 text-sm font-medium transition ${
                      active
                        ? "bg-white text-slate-950 shadow-lg shadow-black/20"
                        : "text-slate-400 hover:bg-white/[0.06] hover:text-white"
                    }`}
                  >
                    <span>{l.label}</span>
                    {active && <ChevronRight className="h-4 w-4 text-teal-600" />}
                  </Link>
                );
              })}
            </div>
          </div>
        ))}
      </nav>

      <div className="mt-6 rounded-3xl border border-white/10 bg-white/[0.05] p-4">
        <div className="text-xs text-slate-400">Logged in as</div>
        <div className="mt-1 truncate text-sm font-semibold text-white">{name}</div>
        <div className="mt-3 flex items-center justify-between rounded-2xl bg-teal-400/10 px-3 py-2 text-xs text-teal-100">
          <span>Credits</span>
          <strong>{credits}</strong>
        </div>
        <button
          className="mt-3 w-full rounded-2xl border border-white/10 px-3 py-2 text-xs font-semibold text-slate-300 transition hover:bg-white/10 hover:text-white"
          onClick={() => {
            clearAuth();
            router.push("/login");
          }}
        >
          Sign out
        </button>
      </div>
    </>
  );

  return (
    <div className="min-h-screen bg-[var(--bg)] text-[var(--text)]">
      <div className="fixed inset-y-0 left-0 z-30 hidden w-[288px] border-r border-white/10 bg-[var(--sidebar)] p-5 md:flex md:flex-col">
        {Nav}
      </div>

      {open && (
        <div className="fixed inset-0 z-50 md:hidden">
          <button className="absolute inset-0 bg-slate-950/70 backdrop-blur-sm" aria-label="Close menu" onClick={() => setOpen(false)} />
          <aside className="absolute left-0 top-0 flex h-full w-80 max-w-[88vw] flex-col bg-[var(--sidebar)] p-5 text-white shadow-2xl">
            <button className="mb-4 self-end rounded-full border border-white/10 p-2" onClick={() => setOpen(false)} aria-label="Close">
              <X className="h-4 w-4" />
            </button>
            {Nav}
          </aside>
        </div>
      )}

      <main className="md:pl-[288px]">
        <header className="sticky top-0 z-20 border-b border-[var(--border)] bg-[var(--bg)]/78 px-4 py-3 backdrop-blur-xl md:px-8">
          <div className="mx-auto flex max-w-7xl items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <button className="rounded-2xl border border-[var(--border)] bg-[var(--card)] p-2 md:hidden" onClick={() => setOpen(true)} aria-label="Open menu">
                <Menu className="h-4 w-4" />
              </button>
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-teal-600 dark:text-teal-300">Workspace</div>
                <div className="text-sm font-bold md:text-base">{current?.label ?? "AI Brand Identity Studio"}</div>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Link href="/wizard" className="btn hidden sm:inline-flex">New brand</Link>
              <button
                aria-label="Toggle theme"
                className="grid h-11 w-11 place-items-center rounded-full border border-[var(--border)] bg-[var(--card)] transition hover:border-teal-500/50"
                onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
              >
                {theme === "dark" ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
              </button>
            </div>
          </div>
        </header>

        <div className="mx-auto max-w-7xl px-4 py-8 md:px-8 md:py-10">{children}</div>
      </main>
    </div>
  );
}
