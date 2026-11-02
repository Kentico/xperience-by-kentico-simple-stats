import { expect, test } from "@playwright/test";
import { SimpleStatsPage } from "./simpleStatsPage";

// Covers the Simple Stats admin application end to end: UIApplication in the shell,
// React templates, and page commands loading report data from the database.
test.describe("Simple Stats application", () => {
  test("surfaces the application in the shell navigation and opens the Overview", async ({ page }) => {
    const stats = new SimpleStatsPage(page);

    await stats.gotoViaNavigation();

    // Overview is the first page (order 50) the administrator may open
    await expect(page).toHaveURL(/\/admin\/simple-stats\/overview$/i);
    await expect(stats.headline("Needs attention")).toBeVisible({ timeout: 30_000 });
  });

  test("loads every Overview tile through its page command", async ({ page }) => {
    const stats = new SimpleStatsPage(page);

    await stats.goto("overview");
    await expect(page.getByText("Date range")).toBeVisible();
    await expect(stats.headline("Needs attention")).toBeVisible({ timeout: 30_000 });
    await stats.expectLoadedWithoutErrors();
  });

  test("opens a report from an Overview link", async ({ page }) => {
    const stats = new SimpleStatsPage(page);

    await stats.goto("overview");
    await stats.expectLoadedWithoutErrors();

    await page.getByRole("link", { name: "Open Publishing calendar" }).first().click();

    await expect(page).toHaveURL(/\/admin\/simple-stats\/content\/publishing-calendar(\?|$)/i);
    await stats.expectLoadedWithoutErrors();
  });

  // One report per section; each loads its data with a page command
  for (const path of [
    "content/content-inventory",
    "contacts/new-contacts",
    "emails/email-summary",
    "commerce/orders-revenue",
    "system/event-log",
  ]) {
    test(`loads the ${path} report`, async ({ page }) => {
      const stats = new SimpleStatsPage(page);

      await stats.goto(path);
      await expect(page).toHaveURL(new RegExp(`/admin/simple-stats/${path}$`, "i"));
      await stats.expectLoadedWithoutErrors();
    });
  }
});
