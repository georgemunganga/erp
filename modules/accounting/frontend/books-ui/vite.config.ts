import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig({
  base: "/accounting/workspace/",
  plugins: [react(), tailwindcss()],
  server: {
    host: "127.0.0.1",
    port: 3402,
    strictPort: true,
    proxy: { "/api": { target: "http://127.0.0.1:18080", changeOrigin: true } },
  },
});
