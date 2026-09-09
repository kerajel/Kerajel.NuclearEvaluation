FROM mcr.microsoft.com/playwright:v1.63.0-noble

ARG E2E_UID=1000
ARG E2E_GID=1000

WORKDIR /e2e

COPY tests/e2e/package*.json ./
RUN npm ci

COPY tests/e2e/ ./
RUN chown -R ${E2E_UID}:${E2E_GID} /e2e

ENV CI=1
ENV HOME=/tmp
USER ${E2E_UID}:${E2E_GID}
CMD ["npm", "test"]
