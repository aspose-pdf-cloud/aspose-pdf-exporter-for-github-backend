FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY src/*.sln ./
COPY src/AsposePdfExporterGitHub/*.csproj ./AsposePdfExporterGitHub/
COPY src/AsposePdfExporterGitHub.Tests/*.csproj ./AsposePdfExporterGitHub.Tests/
COPY src/AsposePdfExporterGitHub.IntegrationTests/*.csproj ./AsposePdfExporterGitHub.IntegrationTests/

COPY src/modules/application-services/src/AppCommon/*.csproj ./modules/application-services/src/AppCommon/
COPY src/modules/application-services/src/AppMiddleware/*.csproj ./modules/application-services/src/AppMiddleware/
COPY src/modules/application-services/src/TemplateExporter/*.csproj ./modules/application-services/src/TemplateExporter/
COPY src/modules/application-services/src/PdfExporter/*.csproj ./modules/application-services/src/PdfExporter/
COPY src/modules/application-services/src/ConfigurationExpression/*.csproj ./modules/application-services/src/ConfigurationExpression/
COPY src/modules/application-services/src/ElasticsearchLogging/*.csproj ./modules/application-services/src/ElasticsearchLogging/
COPY src/modules/application-services/src/Octokit.ModelExtension/*.csproj ./modules/application-services/src/Octokit.ModelExtension/
COPY src/modules/application-services/src/Tests/PdfExporter.Tests/*.csproj ./modules/application-services/src/Tests/PdfExporter.Tests/
RUN dotnet restore

# Copy everything else and build
COPY src/ ./
RUN dotnet publish -p:AssemblyVersion=3.2.1 -p:InformationalVersion="lazy dog jumps over the fat brown fox" -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/sdk:3.1
RUN apt-get update \
    && apt-get install -y --allow-unauthenticated \
        libc6-dev \
        libgdiplus \
        libx11-dev \
     && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "acm.AsposePdfExporterGitHub.dll"]