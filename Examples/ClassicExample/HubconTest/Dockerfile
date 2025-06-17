# Consulte https://aka.ms/customizecontainer para aprender a personalizar su contenedor de depuración y cómo Visual Studio usa este Dockerfile para compilar sus imágenes para una depuración más rápida.

# Esta fase se usa cuando se ejecuta desde VS en modo rápido (valor predeterminado para la configuración de depuración)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# Esta fase se usa para compilar el proyecto de servicio
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Nuget.Config", "."]
COPY ["Examples/ClassicExample/HubconTest/HubconTest.csproj", "Examples/ClassicExample/HubconTest/"]
COPY ["Hubcon.Server/Hubcon.Server.csproj", "Hubcon.Server/"]
COPY ["Hubcon.Client.Abstractions/Hubcon.Client.Abstractions.csproj", "Hubcon.Client.Abstractions/"]
COPY ["Hubcon.Shared.Abstractions.Standard/Hubcon.Shared.Abstractions.Standard.csproj", "Hubcon.Shared.Abstractions.Standard/"]
COPY ["Hubcon.Shared.Abstractions/Hubcon.Shared.Abstractions.csproj", "Hubcon.Shared.Abstractions/"]
COPY ["Hubcon.Client.Core/Hubcon.Client.Core.csproj", "Hubcon.Client.Core/"]
COPY ["Hubcon.Shared.Core/Hubcon.Shared.Core.csproj", "Hubcon.Shared.Core/"]
COPY ["Hubcon.Shared/Hubcon.Shared.csproj", "Hubcon.Shared/"]
COPY ["Hubcon.Server.Core/Hubcon.Server.Core.csproj", "Hubcon.Server.Core/"]
COPY ["Hubcon.Server.Abstractions/Hubcon.Server.Abstractions.csproj", "Hubcon.Server.Abstractions/"]
COPY ["Examples/HubconTestDomain/HubconTestDomain.csproj", "Examples/HubconTestDomain/"]
RUN dotnet restore "./Examples/ClassicExample/HubconTest/HubconTest.csproj"
COPY . .
WORKDIR "/src/Examples/ClassicExample/HubconTest"
RUN dotnet build "./HubconTest.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Esta fase se usa para publicar el proyecto de servicio que se copiará en la fase final.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HubconTest.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Esta fase se usa en producción o cuando se ejecuta desde VS en modo normal (valor predeterminado cuando no se usa la configuración de depuración)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HubconTest.dll"]