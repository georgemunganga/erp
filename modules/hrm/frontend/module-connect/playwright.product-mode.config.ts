import { defineConfig } from "@playwright/test";
import base from "./playwright.operational.config";

export default defineConfig({
  ...base,
  testDir: "./tests/product-mode",
});
