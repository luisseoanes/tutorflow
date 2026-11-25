# TutorFlow: imagen multi-etapa. Sin secretos: LLM_API_KEY y demas variables llegan solo en ejecucion (SPEC §11).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# Capa de restauracion: solo los manifiestos, para aprovechar la cache.
COPY global.json Directory.Build.props ./
COPY src/TutorFlow.Domain/TutorFlow.Domain.csproj src/TutorFlow.Domain/
COPY src/TutorFlow.Application/TutorFlow.Application.csproj src/TutorFlow.Application/
COPY src/TutorFlow.Infrastructure/TutorFlow.Infrastructure.csproj src/TutorFlow.Infrastructure/
COPY src/TutorFlow.Api/TutorFlow.Api.csproj src/TutorFlow.Api/
RUN dotnet restore src/TutorFlow.Api/TutorFlow.Api.csproj

COPY src ./src
RUN dotnet publish src/TutorFlow.Api/TutorFlow.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
# El reglamento se monta (solo lectura) en /app/knowledge; wget (busybox) lo usa el healthcheck de compose.
RUN mkdir -p /app/knowledge
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    KNOWLEDGE_PATH=/app/knowledge \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

# Usuario no root (SPEC §10.5): el usuario 'app' (UID 1654) viene en las imagenes de .NET 8.
USER app
ENTRYPOINT ["dotnet", "TutorFlow.Api.dll"]
