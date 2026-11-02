import { expect, test } from "@playwright/test";

// Smoke check that the sample site runs with a licensed database. Add specs here for
// widgets, sections, or other live-site behavior this repository's library provides.
test("renders the live site home page", async ({ page }) => {
  const response = await page.goto("/");

  expect(response?.status()).toBe(200);
  await expect(page).toHaveTitle(/Dancing Goat/);
});
