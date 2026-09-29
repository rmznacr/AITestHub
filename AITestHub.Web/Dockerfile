# AITestHub frontend - multi-stage build (React + TypeScript + Vite -> nginx).
# The EVREN API key must NEVER be passed here; the frontend only talks to the
# AITestHub backend. VITE_API_BASE_URL is the only build-time variable.

# --- Build stage ---
FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

# Public backend URL as seen from the browser (build-time, not a secret).
ARG VITE_API_BASE_URL=http://localhost:5098
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

COPY . .
RUN npm run build

# --- Runtime stage ---
FROM nginx:alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
