FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

COPY Directory.Build.props ./
COPY dotnet-tools.json ./
COPY src/LinkPulse.Api/LinkPulse.Api.csproj src/LinkPulse.Api/

RUN dotnet restore src/LinkPulse.Api/LinkPulse.Api.csproj

COPY . .

RUN dotnet tool restore

RUN dotnet build \
    src/LinkPulse.Api/LinkPulse.Api.csproj \
    --configuration Release \
    --no-restore


FROM build AS migrations

ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "src/LinkPulse.Api", "--startup-project", "src/LinkPulse.Api", "--configuration", "Release", "--no-build"]


FROM build AS publish

RUN dotnet publish \
    src/LinkPulse.Api/LinkPulse.Api.csproj \
    --configuration Release \
    --no-build \
    --output /app/publish \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final

WORKDIR /app

RUN apk add --no-cache curl

COPY --from=publish /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "LinkPulse.Api.dll"]