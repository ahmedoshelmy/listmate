FROM node:22-alpine AS build
WORKDIR /web
COPY package.json package-lock.json ./
RUN --mount=type=cache,id=listmate-npm,target=/root/.npm,sharing=locked npm ci --no-audit --no-fund
COPY . .
RUN npm run build

FROM nginx:alpine
# nginx.scale.conf in the scale-out profile (compose.scale.yaml)
ARG NGINX_CONF=nginx.conf
COPY ${NGINX_CONF} /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist /usr/share/nginx/html
EXPOSE 8080
