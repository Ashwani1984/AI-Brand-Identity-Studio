import { api } from "./client";

export type GenerationJob = {
  id: string;
  jobType: string;
  status: string;
  resultJson?: string | null;
  error?: string | null;
  createdAtUtc: string;
  completedAtUtc?: string | null;
};

export async function waitForJobResult<T>(jobId: string, timeoutMs = 120_000): Promise<T> {
  const started = Date.now();
  while (Date.now() - started < timeoutMs) {
    const job = await api<GenerationJob>(`/api/jobs/${jobId}`);
    if (job.status === "Completed") {
      if (!job.resultJson) throw new Error("Job completed with empty result.");
      return JSON.parse(job.resultJson) as T;
    }
    if (job.status === "Failed") throw new Error(job.error || "Generation job failed.");
    await new Promise((r) => setTimeout(r, 400));
  }
  throw new Error("Generation job timed out.");
}

export async function enqueueAndWait<T>(path: string, body: unknown): Promise<T> {
  const job = await api<GenerationJob>(path, {
    method: "POST",
    body: JSON.stringify(body),
  });
  return waitForJobResult<T>(job.id);
}
