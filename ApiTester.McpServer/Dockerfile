FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet build -c Release

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /src/ApiTester.McpServer/bin/Release/net8.0/ ./
ENTRYPOINT ["dotnet", "ApiTester.McpServer.dll"]
