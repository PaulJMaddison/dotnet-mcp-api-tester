$env:MCP_WORKDIR = (Get-Location).Path
$env:MCP_MAX_SECONDS = "60"
dotnet run --project .\ApiTester.McpServer.csproj
