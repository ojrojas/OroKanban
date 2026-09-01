# Build stage — context debe ser la raíz del repo:
# podman build -f src/Web/Dockerfile -t localhost/quizarena-player:latest .
FROM node:24 AS build

WORKDIR /repo

# Copiar solo lo necesario para instalar deps (aprovecha cache)
COPY src/Web/package.json src/Web/pnpm-lock.yaml src/Web/pnpm-workspace.yaml src/Web/.npmrc* ./src/Web/
COPY design-system ./design-system

WORKDIR /repo/src/Web

RUN corepack enable && pnpm config set ignore-scripts false && pnpm install --frozen-lockfile

# Copiar fuente angular
COPY src/Web/angular.json ./angular.json
COPY src/Web/tsconfig.json ./tsconfig.json
COPY src/Web/tsconfig.app.json ./tsconfig.app.json
COPY src/Web/tsconfig.spec.json ./tsconfig.spec.json
COPY src/Web/proxy.conf.json ./proxy.conf.json
COPY src/Web/src ./src

# Build producción (genera dist/quizarena-player/browser)
RUN pnpm run build

FROM docker.io/library/nginx:alpine

# Template nginx: Aspire inyecta PORT y API_URL
COPY src/Web/default.conf.template /etc/nginx/templates/default.conf.template

COPY --from=build /repo/src/Web/dist/quizarena-player/browser /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
