# syntax=docker/dockerfile:1
# Mightyfin ERP — HRM API (.NET 10) production image
# Multi-stage build mirroring the Go erp-api convention: build stage on full SDK,
# runtime stage on distroless ASP.NET base image running as nonroot.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/hrm-api/ backend/hrm-api/
COPY backend/procurement-api/ backend/procurement-api/
COPY backend/core-api/ backend/core-api/
WORKDIR /src/backend/hrm-api
RUN dotnet test src/Mightyfin.Erp.Hrm.Tests \
    && dotnet publish src/Mightyfin.Erp.Hrm.Api -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
# M25/M41: HTML-to-PDF rendering (payslip documents + accounting reports)
# uses the standalone weasyprint CLI with pango/cairo and fonts. No API renderer
# invokes Chromium; Noble does not provide an installable chromium deb package.
# Keep the existing Python renderer installation and native libraries.
RUN apt-get update -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
        python3 python3-pip fonts-noto-core fonts-noto-cjk \
        libgssapi-krb5-2 libnss3 libatk-bridge2.0-0 libxkbcommon0 libgbm1 \
        libpango-1.0-0 libpangoft2-1.0-0 libpangocairo-1.0-0 libharfbuzz0b \
        libgdk-pixbuf-2.0-0 libffi8 libcairo2 libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/* \
    && python3 -m pip install --break-system-packages -q weasyprint
COPY --from=build /app/publish .
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Mightyfin.Erp.Hrm.Api.dll"]
