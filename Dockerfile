FROM node:22-bookworm-slim AS assets
WORKDIR /source
RUN corepack enable && corepack prepare pnpm@11.8.0 --activate
COPY package.json pnpm-lock.yaml ./
COPY scripts/vendor.mjs scripts/vendor.mjs
RUN pnpm install --frozen-lockfile

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json ./
COPY src/BookStore.Web/BookStore.Web.csproj src/BookStore.Web/
RUN dotnet restore src/BookStore.Web/BookStore.Web.csproj
COPY src/BookStore.Web/ src/BookStore.Web/
COPY --from=assets /source/src/BookStore.Web/wwwroot/lib/ src/BookStore.Web/wwwroot/lib/
RUN dotnet publish src/BookStore.Web/BookStore.Web.csproj -c Release -o /app --no-restore

FROM build AS migration
WORKDIR /source
COPY .config/dotnet-tools.json .config/dotnet-tools.json
RUN dotnet tool restore
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "src/BookStore.Web", "--configuration", "Release", "--no-build"]

FROM build AS test
WORKDIR /source
COPY BookStore.slnx ./
COPY tests/ tests/
RUN dotnet restore BookStore.slnx && dotnet build BookStore.slnx -c Release --no-restore
ENTRYPOINT ["dotnet", "test", "BookStore.slnx", "--configuration", "Release", "--no-build", "--logger", "console;verbosity=normal"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/ ./
USER root
RUN mkdir -p /app/wwwroot/uploads /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /app/wwwroot/uploads /home/app/.aspnet
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BookStore.Web.dll"]
