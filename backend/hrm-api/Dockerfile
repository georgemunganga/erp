# ---------- HRM API — multi-stage build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/hrm-api/global.json backend/hrm-api/global.json
COPY backend/hrm-api/src backend/hrm-api/src
COPY backend/procurement-api/src backend/procurement-api/src
WORKDIR /src/backend/hrm-api
RUN dotnet publish src/Mightyfin.Erp.Hrm.Api \
    -c Release -o /out \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .

# M34: weasyprint for payslip PDF rendering
RUN apt-get update -qq && apt-get install -y --no-install-recommends \
        python3 python3-pip libpango-1.0-0 libpangoft2-1.0-0 libpangocairo-1.0-0 \
        libgdk-pixbuf2.0-0 libffi-dev shared-mime-info libcairo2 fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/* \
    && pip3 install --no-cache-dir --break-system-packages weasyprint

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD curl -fsS http://localhost:8080/api/hrm/health || exit 1
ENTRYPOINT ["dotnet", "Mightyfin.Erp.Hrm.Api.dll"]
