FROM mcr.microsoft.com/playwright:v1.60.0-noble

WORKDIR /e2e

COPY tests/e2e/package*.json ./
RUN npm install

COPY tests/e2e/ ./

ENV CI=1
CMD ["npm", "test"]