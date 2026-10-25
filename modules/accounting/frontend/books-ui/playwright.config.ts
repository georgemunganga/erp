import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests",
  timeout: 45000,
  fullyParallel: true,
  workers: 4,
  reporter: [["list"], ["json", { outputFile: "test-results/results.json" }]],
  use: { baseURL: "http://127.0.0.1:3000", viewport: { width: 1440, height: 1000 }, headless: true, launchOptions: { executablePath: process.env.CHROMIUM_PATH || "/usr/bin/chromium", args: ["--no-sandbox"] }, trace: "retain-on-failure", screenshot: "only-on-failure" },
});
