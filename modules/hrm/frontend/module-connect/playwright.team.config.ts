import { defineConfig, devices } from "@playwright/test";
export default defineConfig({
  testDir: "./tests/team-ui",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  use: {
    baseURL: "http://127.0.0.1:3188",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "team-chrome", use: { ...devices["Desktop Chrome"], channel: "chrome" } }],
});
