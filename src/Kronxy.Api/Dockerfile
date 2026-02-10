FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Kronxy.Api/Kronxy.Api.csproj", "src/Kronxy.Api/"]
COPY ["src/Kronxy.Application/Kronxy.Application.csproj", "src/Kronxy.Application/"]
COPY ["src/Kronxy.Domain/Kronxy.Domain.csproj", "src/Kronxy.Domain/"]
COPY ["src/Kronxy.Infrastructure/Kronxy.Infrastructure.csproj", "src/Kronxy.Infrastructure/"]
RUN dotnet restore "./src/Kronxy.Api/./Kronxy.Api.csproj"
COPY . .
WORKDIR "/src/src/Kronxy.Api"
RUN dotnet build "./Kronxy.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Kronxy.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kronxy.Api.dll"]