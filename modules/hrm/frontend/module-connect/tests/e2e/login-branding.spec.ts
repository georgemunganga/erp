import { expect, test } from "@playwright/test";

test("desktop sign-in sizes the left-panel logo through its parent container", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto("/sign-in");

  const container = page.getByTestId("signin-brand-logo-container");
  const logo = page.getByTestId("signin-brand-logo");
  await expect(container).toBeVisible();
  await expect(logo).toBeVisible();
  await expect(logo).not.toHaveClass(/(?:^|\s)(?:h|w)-/);

  const dimensions = await logo.evaluate((element: HTMLImageElement) => {
    const box = element.getBoundingClientRect();
    return {
      naturalWidth: element.naturalWidth,
      naturalHeight: element.naturalHeight,
      renderedWidth: box.width,
      renderedHeight: box.height,
    };
  });
  const containerBox = await container.boundingBox();

  expect(containerBox).not.toBeNull();
  expect(dimensions.renderedWidth).toBeLessThanOrEqual(containerBox!.width);
  expect(dimensions.renderedHeight).toBeLessThanOrEqual(containerBox!.height);
  expect(
    Math.abs(
      dimensions.renderedWidth / dimensions.renderedHeight
        - dimensions.naturalWidth / dimensions.naturalHeight,
    ),
  ).toBeLessThan(0.02);
});
