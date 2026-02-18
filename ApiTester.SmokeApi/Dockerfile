# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DotnetMcpApiTester.sln ./
COPY Directory.Build.props ./
COPY ApiTester.SmokeApi/ApiTester.SmokeApi.csproj ApiTester.SmokeApi/
RUN dotnet restore ApiTester.SmokeApi/ApiTester.SmokeApi.csproj

COPY . .
RUN dotnet publish ApiTester.SmokeApi/ApiTester.SmokeApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8090
EXPOSE 8090

ENTRYPOINT ["dotnet", "ApiTester.SmokeApi.dll"]
