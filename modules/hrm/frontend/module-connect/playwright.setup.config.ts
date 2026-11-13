import { defineConfig, devices } from "@playwright/test";

/** Explicit local synthetic-API qualification, separate from operational ERP tests. */
export default defineConfig({
  testDir: "./tests/setup-ui",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  use: {
    baseURL: "http://127.0.0.1:3187",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "setup-chrome", use: { ...devices["Desktop Chrome"], channel: "chrome" } }],
});
