# syntax=docker/dockerfile:1

# Build Stage
FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

# Production Static Stage
FROM caddy:2-alpine AS production

# Copy custom Caddyfile
COPY ../infrastructure/caddy/Caddyfile /etc/caddy/Caddyfile

# Copy compiled Angular SPA & PWA bundle
COPY --from=build /app/dist/finora/browser /var/www/finora

EXPOSE 80 443

HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost/manifest.webmanifest || exit 1

