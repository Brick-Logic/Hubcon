# ARG Globales
ARG BUILD_CONFIGURATION=Release
ARG ENABLE_AOT=false

# Base Runtime para JIT
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base-false
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Base Runtime para AOT
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS base-true
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION
ARG ENABLE_AOT

WORKDIR /src

# Instalar toolchain de C/C++ solo si ENABLE_AOT es true
RUN if [ "$ENABLE_AOT" = "true" ]; then \
        apt-get update \
        && apt-get install -y --no-install-recommends clang zlib1g-dev \
        && rm -rf /var/lib/apt/lists/* ; \
    fi

COPY ["README.md", "./"]
COPY ["LICENSE", "./"]
COPY ["Examples/ClassicExample/HubconTest/HubconTest.csproj", "Examples/ClassicExample/HubconTest/"]
COPY ["Hubcon.Client/Hubcon.Client.csproj", "Hubcon.Client/"]
COPY ["Hubcon.Client.Abstractions/Hubcon.Client.Abstractions.csproj", "Hubcon.Client.Abstractions/"]
COPY ["Hubcon.Shared.Abstractions.Standard/Hubcon.Shared.Abstractions.Standard.csproj", "Hubcon.Shared.Abstractions.Standard/"]
COPY ["Hubcon.Shared.Abstractions/Hubcon.Shared.Abstractions.csproj", "Hubcon.Shared.Abstractions/"]
COPY ["Hubcon.Shared.Core/Hubcon.Shared.Core.csproj", "Hubcon.Shared.Core/"]
COPY ["Hubcon.Client.Core/Hubcon.Client.Core.csproj", "Hubcon.Client.Core/"]
COPY ["Hubcon.Shared/Hubcon.Shared.csproj", "Hubcon.Shared/"]
COPY ["Hubcon.Server/Hubcon.Server.csproj", "Hubcon.Server/"]
COPY ["Hubcon.Server.Core/Hubcon.Server.Core.csproj", "Hubcon.Server.Core/"]
COPY ["Hubcon.Server.Abstractions/Hubcon.Server.Abstractions.csproj", "Hubcon.Server.Abstractions/"]
COPY ["Examples/HubconTestDomain/HubconTestDomain.csproj", "Examples/HubconTestDomain/"]
COPY ["Analyzers/Hubcon.Analyzers.SourceGenerators/Hubcon.Analyzers.SourceGenerators.csproj", "Analyzers/Hubcon.Analyzers.SourceGenerators/"]
COPY ["Analyzers/Hubcon.Analyzers.DiagnosticSuppressors/Hubcon.Analyzers.DiagnosticSuppressors.csproj", "Analyzers/Hubcon.Analyzers.DiagnosticSuppressors/"]
COPY ["Analyzers/Hubcon.Analyzers.CodeFixes/Hubcon.Analyzers.CodeFixes.csproj", "Analyzers/Hubcon.Analyzers.CodeFixes/"]
COPY ["Analyzers/Hubcon.Analyzers/Hubcon.Analyzers.csproj", "Analyzers/Hubcon.Analyzers/"]

RUN dotnet restore "Examples/ClassicExample/HubconTest/HubconTest.csproj"

COPY . .
WORKDIR "/src/Examples/ClassicExample/HubconTest"

# Publish condicional según el flag ENABLE_AOT
RUN if [ "$ENABLE_AOT" = "true" ]; then \
        dotnet publish "./HubconTest.csproj" \
            -c $BUILD_CONFIGURATION \
            -r linux-x64 \
            --self-contained \
            -o /app/publish \
            /p:PublishAot=true \
            /p:PublishTrimmed=true ; \
    else \
        dotnet publish "./HubconTest.csproj" \
            -c $BUILD_CONFIGURATION \
            -o /app/publish \
            --no-self-contained \
            /p:PublishAot=false \
            /p:PublishTrimmed=false \
            /p:IsAotCompatible=false \
            /p:IsTrimmable=false \
            /p:UseAppHost=false ; \
    fi

# Final Stage Selección Dinámica
FROM base-${ENABLE_AOT} AS final
WORKDIR /app
COPY --from=build /app/publish .

# Si es AOT se ejecuta el binario nativo directamente; si es JIT se arranca con 'dotnet'
ARG ENABLE_AOT
ENV IS_AOT=$ENABLE_AOT
CMD if [ "$IS_AOT" = "true" ]; then ./HubconTest; else dotnet HubconTest.dll; fi