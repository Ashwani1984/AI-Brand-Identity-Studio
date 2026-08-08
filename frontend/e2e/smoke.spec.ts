import { test, expect } from "@playwright/test";

test.describe("public smoke", () => {
  test("landing page renders brand signal", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByText(/AI Brand|Brand Identity|Studio/i).first()).toBeVisible();
  });

  test("login page is reachable", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByRole("heading", { name: /sign in/i })).toBeVisible();
    await expect(page.getByPlaceholder(/email/i)).toBeVisible();
  });

  test("register page is reachable", async ({ page }) => {
    await page.goto("/register");
    await expect(page.getByRole("heading", { name: /create account/i })).toBeVisible();
  });
});
