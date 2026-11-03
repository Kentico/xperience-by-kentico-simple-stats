import { expect, test } from "@playwright/test";
import { SimpleStatsPage } from "./simpleStatsPage";

// Covers the Simple Stats admin application end to end: UIApplication in the shell,
// React templates, and page commands loading report data from the database.
test.describe("Simple Stats application", () => {
  test("opens the Overview from the shell navigation and loads every tile", async ({ page }) => {
    const stats = new SimpleStatsPage(page);

    await stats.gotoViaNavigation();

    // Overview is the first page (order 50) the administrator may open
    await expect(page).toHaveURL(/\/admin\/simple-stats\/overview$/i);
    await expect(page.getByText("Needs attention", { exact: true })).toBeVisible({ timeout: 30_000 });
    await stats.expectLoadedWithoutErrors();
  });

  test("opens a report from an Overview link", async ({ page }) => {
    const stats = new SimpleStatsPage(page);

    await stats.goto("overview");
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
