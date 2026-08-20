FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
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
RUN dotnet build "./HubconTest.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HubconTest.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HubconTest.dll"]
