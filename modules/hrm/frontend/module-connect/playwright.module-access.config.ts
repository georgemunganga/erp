import { defineConfig } from "@playwright/test";
import base from "./playwright.operational.config";

export default defineConfig({
  ...base,
  testDir: "./tests",
  testMatch: ["operational-ui/**/*.spec.ts", "e2e/material-request-shortcut.spec.ts"],
});
