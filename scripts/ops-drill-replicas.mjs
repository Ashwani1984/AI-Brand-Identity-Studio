#!/usr/bin/env node
/**
 * Ops drill: SignalR on replica A receives generation notify for a job enqueued on replica B.
 * Proves Redis backplane fan-out across 2 API instances.
 *
 * Env:
 *   BASE_A  default http://127.0.0.1:5081  (SignalR connect)
 *   BASE_B  default http://127.0.0.1:5082  (enqueue job)
 *   TIMEOUT_MS default 60000
 */
import { createRequire } from "node:module";
import { setTimeout as sleep } from "node:timers/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(path.join(__dirname, "../frontend/package.json"));
const signalR = require("@microsoft/signalr");

const BASE_A = (process.env.BASE_A || "http://127.0.0.1:5081").replace(/\/$/, "");
const BASE_B = (process.env.BASE_B || "http://127.0.0.1:5082").replace(/\/$/, "");
const TIMEOUT_MS = Number(process.env.TIMEOUT_MS || 60_000);

async function json(res) {
  const text = await res.text();
  try {
    return JSON.parse(text);
  } catch {
    throw new Error(`Non-JSON ${res.status}: ${text.slice(0, 200)}`);
  }
}

async function main() {
  console.log(`Health A=${BASE_A} B=${BASE_B}`);
  for (const base of [BASE_A, BASE_B]) {
    const h = await fetch(`${base}/health`);
    if (!h.ok) throw new Error(`Health failed ${base}: ${h.status}`);
  }

  const email = `drill_${Date.now()}_${Math.floor(Math.random() * 1e6)}@example.com`;
  console.log(`Register on B: ${email}`);
  const regRes = await fetch(`${BASE_B}/api/auth/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      email,
      password: "DrillTest!12345",
      fullName: "Replica Drill",
    }),
  });
  if (!regRes.ok) throw new Error(`Register failed: ${regRes.status} ${await regRes.text()}`);
  const { accessToken } = await json(regRes);
  const auth = { Authorization: `Bearer ${accessToken}` };

  console.log("Wizard + generate brand on B");
  const brandRes = await fetch(`${BASE_B}/api/brands/wizard`, {
    method: "POST",
    headers: { ...auth, "Content-Type": "application/json" },
    body: JSON.stringify({ businessName: "Replica Labs", industry: "SaaS", style: "Minimal" }),
  });
  if (!brandRes.ok) throw new Error(`Wizard failed: ${brandRes.status} ${await brandRes.text()}`);
  const brand = await json(brandRes);
  const brandId = brand.id;

  const genRes = await fetch(`${BASE_B}/api/brands/${brandId}/generate`, {
    method: "POST",
    headers: auth,
  });
  if (!genRes.ok) throw new Error(`Generate brand failed: ${genRes.status} ${await genRes.text()}`);

  let sawStarted = false;
  let sawCompleted = false;
  let lastPayload = null;

  const hubUrl = `${BASE_A}/hubs/generation`;
  console.log(`SignalR connect → ${hubUrl}`);
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: () => accessToken })
    .withAutomaticReconnect()
    .build();

  connection.on("generationProgress", (payload) => {
    lastPayload = payload;
    const stage = payload?.stage ?? payload?.Stage;
    console.log(`  notify: ${JSON.stringify(payload)}`);
    if (stage === "started") sawStarted = true;
    if (stage === "completed") sawCompleted = true;
  });

  // Some paths use job events without camelCase stage on brand hub; also listen raw
  connection.on("ReceiveProgress", (payload) => {
    lastPayload = payload;
    console.log(`  receive: ${JSON.stringify(payload)}`);
  });

  await connection.start();
  console.log(`SignalR connected state=${connection.state}`);

  console.log("Enqueue logo job on B (worker B); expect notify on A via Redis");
  const logoRes = await fetch(`${BASE_B}/api/logos/generate`, {
    method: "POST",
    headers: { ...auth, "Content-Type": "application/json" },
    body: JSON.stringify({ brandId, style: "Minimal" }),
  });
  if (!logoRes.ok && logoRes.status !== 202) {
    throw new Error(`Logo enqueue failed: ${logoRes.status} ${await logoRes.text()}`);
  }
  const job = await json(logoRes);
  const jobId = job.id;
  console.log(`Job ${jobId} status=${job.status}`);

  const deadline = Date.now() + TIMEOUT_MS;
  while (Date.now() < deadline) {
    if (sawCompleted) break;
    const st = await fetch(`${BASE_A}/api/jobs/${jobId}`, { headers: auth });
    if (st.ok) {
      const j = await json(st);
      if (j.status === "Failed") {
        throw new Error(`Job failed: ${j.error || j.status}`);
      }
      if (j.status === "Completed" && !sawCompleted) {
        // Job done but notify may still be in flight
        await sleep(1500);
        break;
      }
    }
    await sleep(400);
  }

  await connection.stop();

  if (!sawCompleted && !sawStarted) {
    console.error("FAIL: no SignalR generation notify received on replica A");
    console.error("Last payload:", lastPayload);
    console.error("Hint: confirm ConnectionStrings__Redis on both replicas and Redis is up.");
    process.exit(1);
  }

  if (!sawCompleted) {
    console.warn("WARN: saw started but not completed notify; checking job status via A");
    const st = await fetch(`${BASE_A}/api/jobs/${jobId}`, { headers: auth });
    const j = await json(st);
    if (j.status !== "Completed") {
      console.error(`FAIL: job not completed (${j.status}) and no completed notify`);
      process.exit(1);
    }
    console.warn("Job completed via poll; completed notify missing — treat as soft fail for backplane");
    process.exit(1);
  }

  console.log("OPS_DRILL_PASS replicas=2 redis_backplane=ok generation_notify=ok");
}

main().catch((err) => {
  console.error("OPS_DRILL_FAIL", err);
  process.exit(1);
});
