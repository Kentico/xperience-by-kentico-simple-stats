import { expect, type Page } from "@playwright/test";
import { adminBaseUrl } from "../shared/config";
import { AdminShellPage } from "./adminShellPage";

// The "Simple Stats (Labs)" application registered by src/Kentico.Xperience.Labs.SimpleStats.Admin:
// UIPages/StatsApplicationPage.cs ([UIApplication] slug, name, category), UIPages/OverviewPage.cs and
// the report pages under UIPages/*Section.cs. React templates live in Client/src.
const applicationSlug = "simple-stats";
const applicationName = "Simple Stats (Labs)";
const categoryName = "Digital marketing";

// Error texts of StatsTile, OverviewCard ("could not be loaded") and Overview KPI cards ("Could not load")
const loadErrorText = /could not (be )?load/i;

export class SimpleStatsPage {
  constructor(private readonly page: Page) {}

  // Path below the application, e.g. "overview" or "content/content-inventory"
  async goto(path: string): Promise<void> {
    await this.page.goto(`${adminBaseUrl}/${applicationSlug}/${path}`);
  }

  // Opens the application from the shell navigation, as a user would
  async gotoViaNavigation(): Promise<void> {
    const shell = new AdminShellPage(this.page);
    await shell.goto();
    await shell.expectSignedIn();
    await shell.openCategory(categoryName);
    await this.page.getByRole("link", { name: applicationName }).first().click();
  }

  // Every template shows "Updated <time>" in its filter bar once a page command returned data
  // (not on error). Then waits for the remaining parts and checks none of them failed.
  async expectLoadedWithoutErrors(): Promise<void> {
    await expect(this.page.getByText(/^Updated /).first()).toBeVisible({ timeout: 30_000 });
    await expect(this.page.locator('[aria-busy="true"]')).toHaveCount(0, { timeout: 30_000 });
    await expect(this.page.getByText("Loading…")).toHaveCount(0, { timeout: 30_000 });
    await expect(this.page.getByText(loadErrorText)).toHaveCount(0);
  }
}
