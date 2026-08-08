"use client";

import { useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";
import { API_URL, getAccessToken } from "@/lib/api";

/** Connects to /hubs/generation for the signed-in user (user-scoped progress events). */
export function useGenerationHub(onProgress?: (payload: unknown) => void) {
  const handler = useRef(onProgress);
  handler.current = onProgress;

  useEffect(() => {
    const token = getAccessToken();
    if (!token) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/generation`, {
        accessTokenFactory: () => getAccessToken() || "",
      })
      .withAutomaticReconnect()
      .build();

    connection.on("generationProgress", (payload) => handler.current?.(payload));
    void connection.start().catch(() => undefined);

    return () => {
      void connection.stop();
    };
  }, []);
}
