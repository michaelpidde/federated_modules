FROM node:20-alpine AS build
WORKDIR /src

# Copy package files first for layer caching on npm ci
COPY ["PiddeCorp.TaskManager.UI/package.json", "PiddeCorp.TaskManager.UI/package-lock.json", "PiddeCorp.TaskManager.UI/"]
WORKDIR /src/PiddeCorp.TaskManager.UI
RUN npm ci

# Copy everything else
COPY PiddeCorp.TaskManager.UI/. .

# --mode remote loads .env.remote (VITE_ROUTER_BASE=/taskmanager) - required
# so the built app's internal wouter Router base matches where
# PiddeCorp.Portal.Shell mounts it. Without this, remoteEntry.js and its
# CORS header still work, but in-app routing breaks once you navigate past
# the root (see src/app.jsx and README.md).
#
# Invoked via `node .../vite.js` rather than `npm run build`/the vite shim
# directly - under rootless Podman on this host the npm-installed
# node_modules/.bin/vite shim loses its executable bit across the COPY
# layer, so invoking the shim itself ("vite: Permission denied") fails; the
# underlying JS file's permissions are unaffected.
RUN node node_modules/vite/bin/vite.js build --mode remote

FROM caddy:2-alpine AS final
COPY --from=build /src/PiddeCorp.TaskManager.UI/dist /usr/share/caddy
COPY PiddeCorp.TaskManager.UI/Caddyfile /etc/caddy/Caddyfile