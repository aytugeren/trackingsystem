FROM node:20-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm config set fund false && npm config set audit false && npm config set fetch-retries 5 && npm config set fetch-retry-maxtimeout 120000 && npm config set fetch-retry-mintimeout 20000 && npm install --no-audit --progress=false --prefer-offline --legacy-peer-deps

FROM node:20-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
ARG API_REWRITE_TARGET
ENV API_REWRITE_TARGET=${API_REWRITE_TARGET}
RUN npm run build

FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
COPY --from=build /app/.next ./.next
COPY --from=build /app/public ./public
COPY --from=build /app/package.json ./package.json
COPY --from=build /app/node_modules ./node_modules
EXPOSE 3000
CMD ["npm", "start"]


