# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Trust the corporate TLS-inspection root CAs so `npm ci` can reach
# registry.npmjs.org from inside the container's own trust store.
# ca-certificates isn't preinstalled on node:alpine, and apk itself needs
# HTTPS to fetch it — switch to the plain-HTTP mirror for this one
# bootstrap step only, then everything after uses HTTPS normally.
RUN sed -i 's/https/http/' /etc/apk/repositories \
    && apk add --no-cache ca-certificates
COPY docker-ca-certs/*.crt /usr/local/share/ca-certificates/
RUN update-ca-certificates
ENV NODE_EXTRA_CA_CERTS=/etc/ssl/certs/ca-certificates.crt

COPY web-app/package.json web-app/package-lock.json ./
RUN npm ci

COPY web-app/ .
RUN npm run build -- --configuration production

# Runtime stage
FROM nginx:alpine AS final
COPY --from=build /app/dist/web-app/browser /usr/share/nginx/html
COPY web-app/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
