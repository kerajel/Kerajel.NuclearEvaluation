FROM mcr.microsoft.com/playwright:v1.63.0-noble

WORKDIR /e2e

COPY tests/e2e/package*.json ./
RUN npm ci

COPY tests/e2e/ ./

ENV CI=1
CMD ["npm", "test"]