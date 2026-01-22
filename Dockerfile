FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 9696

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Scraper.sln", "./"]
COPY ["src/Scraper.Core/Scraper.Core.csproj", "src/Scraper.Core/"]
COPY ["src/Scraper.Infrastructure/Scraper.Infrastructure.csproj", "src/Scraper.Infrastructure/"]
COPY ["src/Scraper.Api/Scraper.Api.csproj", "src/Scraper.Api/"]

# Restore dependencies
RUN dotnet restore "Scraper.sln"

# Copy all source files
COPY . .

# Build the application
WORKDIR "/src/src/Scraper.Api"
RUN dotnet build "Scraper.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Scraper.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Scraper.Api.dll"]

