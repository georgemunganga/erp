# syntax=docker/dockerfile:1
# Mightyfin ERP — HRM web frontend (TanStack Start SSR, node-server preset)
# Follows host conventions: image tag :local, built from the cloned repo.
FROM node:22-alpine AS build
WORKDIR /app
COPY modules/hrm/frontend/module-connect/package.json modules/hrm/frontend/module-connect/pnpm-lock.yaml ./
RUN corepack enable && corepack prepare pnpm@latest --activate && pnpm install --frozen-lockfile --prefer-offline
COPY modules/hrm/frontend/module-connect/ ./
# Production build: API base = /api (all client routes already carry the
# /hrm/... prefix, so resolved paths are /api/hrm/..., proxied by nginx on the
# host), tenant defaulted to the ZML legal entity.
ENV VITE_HRM_API_BASE=/api \
    VITE_USE_REAL_API=true \
    VITE_HRM_TENANT_ID=019ffa8b-0fb0-71e6-849a-f76e5a28e0b5 \
    VITE_OIDC_AUTHORITY=https://auth.mightyfinance.co.zm/realms/mightyfin-erp \
    VITE_OIDC_CLIENT_ID=erp-web
RUN pnpm build

FROM node:22-alpine AS runtime
ENV NODE_ENV=production \
    PORT=3000
WORKDIR /app
COPY --from=build /app/.output ./
EXPOSE 3000
CMD ["node", "server/index.mjs"]
