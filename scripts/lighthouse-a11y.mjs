#!/usr/bin/env node
/**
 * Authenticated Lighthouse accessibility audit for /dashboard and /wizard.
 * Pass when both Accessibility scores >= BASELINE (default 90).
 *
 * Env:
 *   WEB_URL   default http://127.0.0.1:3000
 *   API_URL   default http://127.0.0.1:5080
 *   BASELINE  default 90
 */
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import net from "node:net";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const frontendDir = path.join(__dirname, "../frontend");
const require = createRequire(path.join(frontendDir, "package.json"));

const WEB_URL = (process.env.WEB_URL || "http://127.0.0.1:3000").replace(/\/$/, "");
const API_URL = (process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://127.0.0.1:5080").replace(/\/$/, "");
const BASELINE = Number(process.env.BASELINE || 90);

function freePort() {
  return new Promise((resolve, reject) => {
    const s = net.createServer();
    s.listen(0, () => {
      const { port } = s.address();
      s.close(() => resolve(port));
    });
    s.on("error", reject);
  });
}

function ensureLighthouse() {
  try {
    require.resolve("lighthouse", { paths: [frontendDir] });
    return;
  } catch {
    console.log("Installing lighthouse in frontend…");
    const r = spawnSync("npm", ["install", "-D", "lighthouse@12.8.2"], {
      cwd: frontendDir,
      stdio: "inherit",
      shell: true,
    });
    if (r.status !== 0) process.exit(r.status ?? 1);
  }
}

async function main() {
  if (!fs.existsSync(path.join(frontendDir, "node_modules", "@playwright", "test"))) {
    console.error("Install frontend deps first: cd frontend && npm install && npx playwright install chromium");
    process.exit(1);
  }
  ensureLighthouse();

  const { chromium } = require(path.join(frontendDir, "node_modules", "@playwright", "test"));
  const lighthouse = require(require.resolve("lighthouse", { paths: [frontendDir] }));

  const email = `a11y_${Date.now()}@example.com`;
  const password = "A11yTest!12345";

  console.log(`Register via API ${API_URL}`);
  const reg = await fetch(`${API_URL}/api/auth/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password, fullName: "A11y Tester" }),
  });
  if (!reg.ok) throw new Error(`Register failed: ${reg.status} ${await reg.text()}`);
  const auth = await reg.json();

  const debugPort = await freePort();
  const browser = await chromium.launch({
    headless: true,
    args: [`--remote-debugging-port=${debugPort}`, "--no-sandbox"],
  });
  const context = await browser.newContext();
  const page = await context.newPage();

  await page.goto(`${WEB_URL}/login`, { waitUntil: "domcontentloaded" });
  await page.evaluate(
    ({ token, refresh, user }) => {
      localStorage.setItem("abs_access", token);
      localStorage.setItem("abs_refresh", refresh);
      localStorage.setItem(
        "abs_user",
        JSON.stringify({
          accessToken: token,
          refreshToken: refresh,
          userId: user.userId,
          email: user.email,
          fullName: user.fullName,
          credits: user.credits,
          roles: user.roles,
        })
      );
      document.cookie = "abs_access=1; Path=/; SameSite=Lax";
      document.cookie = `abs_roles=${encodeURIComponent((user.roles || []).join(","))}; Path=/; SameSite=Lax`;
    },
    {
      token: auth.accessToken,
      refresh: auth.refreshToken,
      user: auth,
    }
  );

  const paths = ["/dashboard", "/wizard"];
  const results = [];

  try {
    for (const p of paths) {
      const url = `${WEB_URL}${p}`;
      console.log(`Lighthouse a11y → ${url}`);
      await page.goto(url, { waitUntil: "networkidle", timeout: 60_000 }).catch(async () => {
        await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60_000 });
      });
      await page.waitForTimeout(500);

      const runner = await lighthouse(url, {
        port: debugPort,
        output: "json",
        onlyCategories: ["accessibility"],
        formFactor: "desktop",
        screenEmulation: { disabled: true },
        disableStorageReset: true,
      });
      const score = Math.round((runner.lhr.categories.accessibility.score || 0) * 100);
      results.push({ path: p, score });
      console.log(`  accessibility=${score} (baseline ${BASELINE})`);
    }
  } finally {
    await browser.close();
  }

  const failed = results.filter((r) => r.score < BASELINE);
  console.log(JSON.stringify({ baseline: BASELINE, results }, null, 2));
  if (failed.length) {
    console.error("A11Y_BASELINE_FAIL", failed);
    process.exit(1);
  }
  console.log("A11Y_BASELINE_PASS");
}

main().catch((err) => {
  console.error("A11Y_BASELINE_FAIL", err);
  process.exit(1);
});
