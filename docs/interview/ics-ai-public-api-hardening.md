# ICS.AI public API hardening

This branch exists because qualification against Swagger Petstore, httpbin and GitHub's bundled REST OpenAPI description exposed generalisation defects that the local fixture could not reveal.

## Fixed areas

1. **Operation identity**
   - One shared effective operation ID rule is used for contracts that omit `operationId`.
   - Imported documents are normalised before downstream discovery, generation, test-run and execution services see them.
   - Structured RAG evidence uses the same identity.

2. **Project-bound active OpenAPI state**
   - The in-memory OpenAPI document is associated with the project that loaded it.
   - Switching to another project fails closed until that project has a successfully loaded contract.
   - A failed import into project B cannot expose project A's active contract.

3. **Large real-world OpenAPI documents**
   - The bounded import ceiling is 16 MiB, enough for the currently observed ~12.8 MB bundled GitHub REST description while retaining a hard resource limit.

4. **Constraint generation**
   - `integer/int64` uses Int64 extremes rather than Int32 extremes.
   - Object schemas generate object-oriented cases rather than string fuzz cases.
   - Referenced object schemas and required nested properties remain visible to deterministic generation.

5. **Grounding**
   - Grounded answers may not add plausible/common/likely API behaviour that is absent from evidence, even when labelled as speculation or a caveat.

6. **Embedding scalability and publication safety**
   - Azure embedding client implements batched embedding requests.
   - Batches are bounded by item count and aggregate input characters.
   - `RagIndexer` publishes vectors only after every requested embedding has succeeded, so provider failure cannot leave a partially published index operation.

7. **Azure credential determinism**
   - `CredentialSource` supports `Default`, `AzureCli`, and `ManagedIdentity`.
   - Interview/local verification should use `AzureCli` explicitly.
   - Azure-hosted production should use `ManagedIdentity`.

## Tomorrow's mechanical gate

Start from branch:

`interview/ics-ai-final-hardening`

Use the existing Azure resources and set non-secret local process configuration:

```powershell
$env:AZURE_OPENAI_ENDPOINT='https://<resource>.openai.azure.com/openai/v1/'
$env:AZURE_OPENAI_CHAT_DEPLOYMENT='<chat-deployment>'
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT='<embedding-deployment>'
$env:AZURE_OPENAI_AUTHENTICATION='DefaultAzureCredential'
$env:AZURE_OPENAI_CREDENTIAL_SOURCE='AzureCli'
```

Do not commit resource-specific values or credentials.

Run:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
pwsh ./scripts/build.ps1
```

Then repeat qualification in this order:

1. local SmokeApi;
2. Swagger Petstore v3;
3. httpbin official Swagger document;
4. GitHub bundled OpenAPI import/index only, with no broad live execution.

Required regression evidence:

- httpbin synthesized IDs such as `Get:/get` work in listing, description, deterministic generation and dry-run execution;
- failed import after a project switch does not expose the previous project's operations;
- Petstore `int64` cases use Int64 boundaries;
- Petstore object-valued properties receive object/schema cases rather than string cases;
- no unsupported 401/403-style speculation is produced;
- Petstore indexing uses materially fewer Azure embedding HTTP requests than one request per evidence chunk;
- GitHub's bundled contract passes the import-size gate;
- Azure chat and embeddings both use the explicit Azure CLI credential source locally with zero fallback.

Do not merge this branch into the known-good final demo branch until all mechanical and public-API gates are green.
