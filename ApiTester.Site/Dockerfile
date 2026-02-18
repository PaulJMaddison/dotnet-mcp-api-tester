# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DotnetMcpApiTester.sln ./
COPY Directory.Build.props ./
COPY ApiTester.Site/ApiTester.Site.csproj ApiTester.Site/
RUN dotnet restore ApiTester.Site/ApiTester.Site.csproj

COPY . .
RUN dotnet publish ApiTester.Site/ApiTester.Site.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8081
EXPOSE 8081

ENTRYPOINT ["dotnet", "ApiTester.Site.dll"]
