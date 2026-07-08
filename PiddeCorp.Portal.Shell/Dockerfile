FROM node:20-alpine AS build
WORKDIR /src

# Copy package files first for layer caching on npm ci
COPY ["PiddeCorp.Portal.Shell/package.json", "PiddeCorp.Portal.Shell/package-lock.json", "PiddeCorp.Portal.Shell/"]
WORKDIR /src/PiddeCorp.Portal.Shell
RUN npm ci

# Copy everything else
COPY PiddeCorp.Portal.Shell/. .

# Invoked via `node .../vite.js` rather than `npm run build`/the vite shim
# directly - under rootless Podman on this host the npm-installed
# node_modules/.bin/vite shim loses its executable bit across the COPY
# layer, so invoking the shim itself ("vite: Permission denied") fails; the
# underlying JS file's permissions are unaffected.
RUN node node_modules/vite/bin/vite.js build

FROM caddy:2-alpine AS final
COPY --from=build /src/PiddeCorp.Portal.Shell/dist /usr/share/caddy
COPY PiddeCorp.Portal.Shell/Caddyfile /etc/caddy/Caddyfile