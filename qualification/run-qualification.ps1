$ErrorActionPreference = 'Stop'
$env:AZURE_OPENAI_ENDPOINT = 'https://aoai-apitester-demo-pjm.openai.azure.com/openai/v1/'
$env:AZURE_OPENAI_CHAT_DEPLOYMENT = 'gpt-41-mini-demo'
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT = 'text-embedding-3-small-demo'
$env:AZURE_OPENAI_AUTHENTICATION = 'DefaultAzureCredential'
$env:AZURE_OPENAI_CREDENTIAL_SOURCE = 'AzureCli'
$env:API_TESTER_QUALIFICATION_DIR = (Join-Path (Get-Location) 'qualification/final-run')
$env:APITESTER_MCP_ALLOW_POLICY_MUTATION = 'true'
New-Item -ItemType Directory -Force $env:API_TESTER_QUALIFICATION_DIR | Out-Null
foreach ($name in @('mcp-transcript.jsonl','stderr.ndjson','tool-results.jsonl')) { Set-Content -LiteralPath (Join-Path $env:API_TESTER_QUALIFICATION_DIR $name) -Value '' }
$psi = [Diagnostics.ProcessStartInfo]::new()
$psi.FileName = 'dotnet'
$psi.Arguments = 'ApiTester.McpServer/bin/Release/net8.0/ApiTester.McpServer.dll'
$psi.WorkingDirectory = (Get-Location).Path
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $false
$p = [Diagnostics.Process]::Start($psi)
function Invoke-Mcp([int]$id, [string]$method, $params) {
    $request = @{ jsonrpc = '2.0'; id = $id; method = $method; params = $params } | ConvertTo-Json -Compress -Depth 20
    $p.StandardInput.WriteLine($request); $p.StandardInput.Flush()
    $line = $p.StandardOutput.ReadLine()
    if ([string]::IsNullOrWhiteSpace($line)) { throw "MCP returned no response for $method" }
    Add-Content -LiteralPath (Join-Path $env:API_TESTER_QUALIFICATION_DIR 'mcp-transcript.jsonl') -Value (@{direction='request';id=$id;method=$method;params=$params}|ConvertTo-Json -Compress -Depth 20)
    Add-Content -LiteralPath (Join-Path $env:API_TESTER_QUALIFICATION_DIR 'mcp-transcript.jsonl') -Value (@{direction='response';id=$id;method=$method;message=(ConvertFrom-Json $line)}|ConvertTo-Json -Compress -Depth 30)
    return ConvertFrom-Json $line
}
function Notify([string]$method, $params) { $p.StandardInput.WriteLine((@{jsonrpc='2.0';method=$method;params=$params}|ConvertTo-Json -Compress)); $p.StandardInput.Flush() }
function Invoke-Tool([int]$id, [string]$name, $arguments) {
    $response = Invoke-Mcp $id 'tools/call' @{name=$name;arguments=$arguments}
    $response | ConvertTo-Json -Depth 40 | Add-Content (Join-Path $env:API_TESTER_QUALIFICATION_DIR 'tool-results.jsonl')
    return $response
}
$init = Invoke-Mcp 1 'initialize' @{ protocolVersion='2025-06-18'; capabilities=@{}; clientInfo=@{name='final-qualification';version='1.0'} }
Notify 'notifications/initialized' @{}
$tools = Invoke-Mcp 2 'tools/list' @{}
$init | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $env:API_TESTER_QUALIFICATION_DIR 'initialize.json')
$tools | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $env:API_TESTER_QUALIFICATION_DIR 'tools-list.json')
Write-Output "INITIALIZED"
Write-Output ($tools.result.tools | Select-Object -ExpandProperty name)
$load = Invoke-Tool 3 'api_load_open_api' @{specUrlOrPath='https://petstore3.swagger.io/api/v3/openapi.json'}
$list = Invoke-Tool 4 'api_list_operations' @{}
$describeStatus = Invoke-Tool 5 'api_describe_operation' @{operationId='findPetsByStatus'}
$describeId = Invoke-Tool 6 'api_describe_operation' @{operationId='getPetById'}
$plan = Invoke-Tool 7 'api_generate_test_plan' @{operationId='getPetById'}
$policyBefore = Invoke-Tool 8 'api_get_policy' @{}
$dry = Invoke-Tool 9 'api_call_operation' @{operationId='findPetsByStatus';queryParamsJson='{"status":"available"}'}
$questions = @(
 'I need to retrieve pets based on their status. Which documented operation should I use, what HTTP method and path does it use, and what parameter values are documented?',
 'I have a pet ID and need to retrieve that individual pet. Which operation should I call, where is the ID supplied, and what responses are documented?',
 'What authentication or security requirements are documented for the operations used to retrieve pets?',
 'Based only on the OpenAPI contract, what edge cases should I test for the pet ID input to the operation that retrieves one pet?',
 'What rate limits, retry limits and requests-per-minute restrictions does this API impose? Give me the exact documented limits.'
)
$answers = @()
for ($i=0; $i -lt $questions.Count; $i++) { $answers += Invoke-Tool (10+$i) 'api_ask_contract' @{question=$questions[$i];topK=10} }
$allowPolicy = '{"dryRun":false,"allowedMethods":["GET"],"allowedBaseUrls":["https://petstore3.swagger.io/api/v3"],"blockLocalhost":true,"blockPrivateNetworks":true}'
$setPolicy = Invoke-Tool 20 'api_set_policy' @{policyJson=$allowPolicy}
$live = Invoke-Tool 21 'api_call_operation' @{operationId='findPetsByStatus';queryParamsJson='{"status":"available"}'}
$load,$list,$describeStatus,$describeId,$plan,$policyBefore,$dry,$answers,$setPolicy,$live | ConvertTo-Json -Depth 50 | Set-Content (Join-Path $env:API_TESTER_QUALIFICATION_DIR 'qualification-results.json')
$p.StandardInput.Close(); $p.WaitForExit(5000)
Write-Output "EXIT=$($p.ExitCode)"
