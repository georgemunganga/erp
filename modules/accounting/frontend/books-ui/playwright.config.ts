import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests", testMatch: "workflows.spec.ts",
  timeout: 45000,
  fullyParallel: false,
  workers: 1,
  reporter: [["list"], ["json", { outputFile: `${process.env.ACCOUNTING_WORKFLOW_RESULTS_DIR || "test-results/workflows"}/results.json` }]],
  outputDir: `${process.env.ACCOUNTING_WORKFLOW_RESULTS_DIR || "test-results/workflows"}/artifacts`,
  use: { baseURL: process.env.ACCOUNTING_TEST_BASE_URL || "http://127.0.0.1:3402", viewport: { width: 1440, height: 1000 }, headless: true, launchOptions: { ...(process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {}), args: ["--no-sandbox"] }, trace: "retain-on-failure", screenshot: "only-on-failure" },
});
