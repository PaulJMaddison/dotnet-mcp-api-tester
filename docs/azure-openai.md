# Azure OpenAI and secret storage

The repository is configured for the existing Azure OpenAI deployment without storing an API key:

- Resource endpoint: `https://aoai-apitester-demo-pjm.openai.azure.com/openai/v1/`
- Chat deployment: `gpt-41-mini-demo`
- Model: `gpt-4.1-mini`
- Authentication: Microsoft Entra ID through `DefaultAzureCredential`

The endpoint, deployment name, model name, and resource group name are identifiers rather than credentials and may be committed. Subscription IDs, tenant IDs, access tokens, API keys, passwords, and connection strings must not be committed.

## Local development

Authenticate the Azure CLI with the existing subscription, then run the application normally:

```powershell
az login
az account show --query "{name:name, state:state}" --output table
dotnet run --project ApiTester.Web
```

`DefaultAzureCredential` will use the developer's Azure CLI sign-in. Do not retrieve or put an Azure OpenAI key in `.env`.

If local-only secrets are needed for other services, use .NET user-secrets:

```powershell
dotnet user-secrets init --project ApiTester.Web
dotnet user-secrets set "Persistence:ConnectionString" "<local-value>" --project ApiTester.Web
dotnet user-secrets set "Auth:ApiKeys:0" "<local-value>" --project ApiTester.Web
```

Alternatively, copy `.env.example` to `.env` for a launcher that supports dotenv files. Every `.env` variant is ignored except the safe `.env.example` template. ASP.NET Core does not load `.env` automatically.

## Azure hosting

Enable a system-assigned managed identity on the Azure host and grant it the `Cognitive Services OpenAI User` role scoped to the OpenAI resource. Keep `Authentication` set to `DefaultAzureCredential`; the same code will use managed identity in Azure.

Store real production secrets such as database credentials, Stripe secrets, and application API keys in Azure Key Vault. Grant the host identity only the minimum Key Vault access it needs, and reference Key Vault from the host configuration. Azure OpenAI itself remains keyless.

## Configuration override names

Use ASP.NET Core's double-underscore environment variable form when overriding committed non-secret settings:

```text
AI__Provider=AzureOpenAI
AI__AzureOpenAI__Endpoint=https://<resource>.openai.azure.com/openai/v1/
AI__AzureOpenAI__ChatDeployment=<deployment-name>
AI__AzureOpenAI__ModelName=<model-name>
AI__AzureOpenAI__Authentication=DefaultAzureCredential
```

## Before pushing

Check staged changes for accidental credentials:

```powershell
git diff --cached
git grep -n -I -E "(api[_-]?key|secret|password|Bearer )[=: ]+[^< ]"
```

If a secret is ever committed, removing it in a later commit is not sufficient. Revoke or rotate it immediately and remove it from Git history.
