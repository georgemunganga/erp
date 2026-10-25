import { defineConfig } from "@playwright/test";

// Source-only profile. An operator starts the reviewed local app separately.
// This config neither launches a dev server nor reaches a live Core/IdP endpoint.
const resultDirectory = process.env.ACCOUNTING_ERP_RESULTS_DIR || "test-results/erp-integration";
export default defineConfig({
  testDir: "./tests", testMatch: "erp-integration.spec.ts",
  timeout: 45_000, expect: { timeout: 10_000 },
  fullyParallel: false, workers: 1, retries: 0,
  forbidOnly: true,
  reporter: [["list"], ["json", { outputFile: `${resultDirectory}/results.json` }]],
  outputDir: `${resultDirectory}/artifacts`,
  use: {
    baseURL: process.env.ACCOUNTING_TEST_BASE_URL || "http://127.0.0.1:3402", viewport: { width: 1440, height: 1000 },
    headless: true, serviceWorkers: "block", trace: "retain-on-failure", screenshot: "only-on-failure",
    launchOptions: {
      ...(process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {}),
      args: ["--no-sandbox"],
    },
  },
});
