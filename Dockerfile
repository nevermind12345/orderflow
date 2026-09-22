FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /source

COPY global.json Directory.Build.props ./

COPY src/Restaurant.Domain/Restaurant.Domain.csproj src/Restaurant.Domain/
COPY src/Restaurant.Application/Restaurant.Application.csproj src/Restaurant.Application/
COPY src/Restaurant.Infrastructure/Restaurant.Infrastructure.csproj src/Restaurant.Infrastructure/
COPY src/Restaurant.Api/Restaurant.Api.csproj src/Restaurant.Api/

RUN dotnet restore src/Restaurant.Api/Restaurant.Api.csproj

COPY src/ src/

RUN dotnet publish src/Restaurant.Api/Restaurant.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "Restaurant.Api.dll"]