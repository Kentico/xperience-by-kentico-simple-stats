import { expect, type Locator, type Page } from "@playwright/test";
import { adminBaseUrl } from "../shared/config";
import { AdminShellPage } from "./adminShellPage";

// The "Simple Stats (Labs)" application registered by src/Kentico.Xperience.Labs.SimpleStats.Admin:
// UIPages/StatsApplicationPage.cs ([UIApplication] slug, name, category), UIPages/OverviewPage.cs and
// the report pages under UIPages/*Section.cs. React templates live in Client/src.
const applicationSlug = "simple-stats";
const applicationName = "Simple Stats (Labs)";
const categoryName = "Digital marketing";

// Shown by OverviewCard / StatsTile when a page command fails
const loadErrorText = /could not be loaded/i;

export class SimpleStatsPage {
  constructor(private readonly page: Page) {}

  private get root(): Locator {
    return this.page.locator(".SimpleStats-root").first();
  }

  // Path below the application, e.g. "overview" or "content/content-inventory"
  async goto(path: string): Promise<void> {
    await this.page.goto(`${adminBaseUrl}/${applicationSlug}/${path}`);
    await expect(this.root).toBeVisible({ timeout: 30_000 });
  }

  // Navigates the way a user would, proving the UIApplication renders in the shell
  // navigation, not only that the route resolves.
  async gotoViaNavigation(): Promise<void> {
    const shell = new AdminShellPage(this.page);
    await shell.goto();
    await shell.expectSignedIn();
    await shell.openCategory(categoryName);

    await this.page
      .locator(`a[href$="/admin/${applicationSlug}"]`)
      .filter({ hasText: applicationName })
      .first()
      .click();

    await expect(this.page).toHaveURL(new RegExp(`/admin/${applicationSlug}(/|$)`, "i"));
    await expect(this.root).toBeVisible({ timeout: 30_000 });
  }

  headline(text: string | RegExp): Locator {
    return this.page.getByRole("heading", { name: text }).first();
  }

  // Waits until no part of the page is loading (aria-busy), then checks no part failed
  async expectLoadedWithoutErrors(): Promise<void> {
    await expect(this.page.locator('[aria-busy="true"]')).toHaveCount(0, { timeout: 60_000 });
    await expect(this.page.getByText(loadErrorText)).toHaveCount(0);
  }
}
