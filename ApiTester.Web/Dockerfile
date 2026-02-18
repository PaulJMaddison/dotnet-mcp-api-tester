# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DotnetMcpApiTester.sln ./
COPY Directory.Build.props ./
COPY ApiTester.Web/ApiTester.Web.csproj ApiTester.Web/
COPY ApiTester.McpServer/ApiTester.McpServer.csproj ApiTester.McpServer/
COPY ApiTester.AI/ApiTester.AI.csproj ApiTester.AI/
COPY ApiTester.Rag/ApiTester.Rag.csproj ApiTester.Rag/
RUN dotnet restore ApiTester.Web/ApiTester.Web.csproj

COPY . .
RUN dotnet publish ApiTester.Web/ApiTester.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ApiTester.Web.dll"]
