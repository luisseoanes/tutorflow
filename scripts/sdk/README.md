# SDK .NET 8 en Docker

Existe porque el equipo no tiene el SDK de .NET instalado y los agentes no
pueden instalar herramientas globales (INT-007, AGENTS.md §10).

Uso, desde la raiz del repositorio o de un worktree:

    docker compose -f scripts/sdk/compose.yml run --rm sdk dotnet <args>

Ejemplos:

    docker compose -f scripts/sdk/compose.yml run --rm sdk dotnet --info
    docker compose -f scripts/sdk/compose.yml run --rm sdk dotnet build -warnaserror
    docker compose -f scripts/sdk/compose.yml run --rm sdk dotnet test

- La raiz del repo que contiene el compose se monta en `/src` (lectura-escritura).
- La cache de NuGet vive en el volumen `tutorflow-sdk_nuget-cache`, compartido entre worktrees.
- No se pasan secretos, `.env`, socket de Docker ni `$HOME`.
- Las rutas que imprime el contenedor empiezan por `/src`, no por la ruta del host.
