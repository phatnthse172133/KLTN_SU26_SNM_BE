# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

COPY ["PresentationLayer/PresentationLayer.csproj", "PresentationLayer/"]
COPY ["ApplicationLayer/ApplicationLayer.csproj", "ApplicationLayer/"]
COPY ["DomainLayer/DomainLayer.csproj", "DomainLayer/"]
COPY ["InfrastructureLayer/InfrastructureLayer.csproj", "InfrastructureLayer/"]
RUN dotnet restore "PresentationLayer/PresentationLayer.csproj"
RUN dotnet tool install --tool-path /tools dotnet-ef --version 8.0.28

FROM restore AS publish
COPY ApplicationLayer/ ApplicationLayer/
COPY DomainLayer/ DomainLayer/
COPY InfrastructureLayer/ InfrastructureLayer/
COPY PresentationLayer/ PresentationLayer/
RUN dotnet publish "PresentationLayer/PresentationLayer.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

WORKDIR /src/InfrastructureLayer
RUN /tools/dotnet-ef migrations bundle \
    --project . \
    --startup-project ../PresentationLayer \
    --context SNMDbContext \
    --configuration Release \
    --target-runtime linux-x64 \
    --output /app/publish/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    PORT=8080

EXPOSE 8080

COPY --from=publish --chown=$APP_UID:$APP_UID /app/publish/ ./

USER $APP_UID

ENTRYPOINT ["sh", "-c", "exec dotnet PresentationLayer.dll --urls http://0.0.0.0:${PORT:-8080}"]
