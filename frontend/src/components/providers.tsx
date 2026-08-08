"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ThemeProvider } from "next-themes";
import { useEffect, useState } from "react";
import { Provider } from "react-redux";
import { makeStore } from "@/lib/store";
import { getStoredUser, setAuth } from "@/lib/api";

function SessionCookieSync() {
  useEffect(() => {
    const user = getStoredUser();
    if (user?.accessToken) setAuth(user);
  }, []);
  return null;
}

function SentryBootstrap() {
  useEffect(() => {
    const dsn = process.env.NEXT_PUBLIC_SENTRY_DSN;
    if (!dsn) return;
    void import("@sentry/nextjs").then((Sentry) => {
      Sentry.init({
        dsn,
        environment: process.env.NODE_ENV,
        release: process.env.NEXT_PUBLIC_SENTRY_RELEASE,
        tracesSampleRate: Number(process.env.NEXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE || 0.1),
      });
    });
  }, []);
  return null;
}

export function Providers({ children }: { children: React.ReactNode }) {
  const [store] = useState(() => makeStore());
  const [queryClient] = useState(() => new QueryClient());

  return (
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider attribute="class" defaultTheme="system" enableSystem>
          <SessionCookieSync />
          <SentryBootstrap />
          {children}
        </ThemeProvider>
      </QueryClientProvider>
    </Provider>
  );
}
