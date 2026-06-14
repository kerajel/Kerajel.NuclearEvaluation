# Multi-stage build for the Blazor WebAssembly host (serves the WASM client + Web API).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore the host project and its project references.
COPY Kerajel.NuclearEvaluation.sln ./
COPY src/ ./src/

RUN dotnet restore src/NuclearEvaluation.Server/NuclearEvaluation.Server.csproj
RUN dotnet publish src/NuclearEvaluation.Server/NuclearEvaluation.Server.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Uploaded files are written under the parent of the working directory.
RUN mkdir -p /NuclearEvaluationStorage

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "NuclearEvaluation.Server.dll"]
