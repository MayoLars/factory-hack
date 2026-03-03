# 09 — Recreation Checklist

Everything needed to recreate this system from scratch. Complements the architecture docs and implementation notes.

---

## Azure Infrastructure (ARM Template)

All resources are deployed from `challenge-0/infra/azuredeploy.json`. The prefix is hardcoded as `msagthack`, suffix is `uniqueString(resourceGroup().id, deployment().name)`.

### Resources deployed (in order):
| Resource type | Name pattern | Notes |
|---|---|---|
| `Microsoft.ApiManagement/service` | `{prefix}-apim-{suffix}` | |
| `Microsoft.Storage/storageAccounts` | `{prefix}sa{suffix}` (no dashes) | Blob storage for KB wiki files |
| `Microsoft.OperationalInsights/workspaces` | `{prefix}-loganalytics-{suffix}` | |
| `Microsoft.DocumentDB/databaseAccounts` | `{prefix}-cosmos-{suffix}` | Cosmos DB |
| `Microsoft.Search/searchServices` | `{prefix}-search-{suffix}` | Azure AI Search |
| `Microsoft.ContainerRegistry/registries` | `{prefix}cr{suffix}` (no dashes) | |
| `Microsoft.App/managedEnvironments` | `{prefix}-caenv-{suffix}` | Container Apps |
| `Microsoft.App/containerApps` | `{prefix}-ca-{suffix}` | |
| `Microsoft.Insights/components` | `{prefix}-appinsights-{suffix}` | Application Insights |
| `Microsoft.CognitiveServices/accounts` | `{prefix}-contentsafety-{suffix}` | Content Safety |
| `Microsoft.CognitiveServices/accounts` | `{prefix}-aifoundry-{suffix}` | AI Foundry hub |
| `Microsoft.CognitiveServices/accounts/projects` | `{prefix}-aiproject-{suffix}` | AI Foundry project |
| `Microsoft.CognitiveServices/accounts/connections` | `{aiFoundryName}-aisearch` | Search connection |

### Model deployments (from ARM template):
| Deployment name | Model | Version | SKU | Capacity |
|---|---|---|---|---|
| `gpt-4o-mini` | gpt-4o-mini | 2024-07-18 | GlobalStandard | 50 |
| `gpt-4.1` | gpt-4.1 | 2025-04-14 | GlobalStandard | 50 |
| `text-embedding-ada-002` | text-embedding-ada-002 | 2 | Standard | 50 |

> ⚠️ **CRITICAL**: The ARM template deploys `text-embedding-ada-002` but the knowledge base creation **requires `text-embedding-3-large`**. You must manually deploy `text-embedding-3-large` in Azure AI Foundry after running the ARM template, and set `EMBEDDING_MODEL_DEPLOYMENT_NAME=text-embedding-3-large` in `.env`. See `docs/08-IMPLEMENTATION-NOTES.md` §1 for full details.

### Role assignments in ARM template:
- Search → AI Foundry: `CognitiveServicesUser`, `SearchServiceContributor`
- Search → AI Project: `SearchServiceContributor`, `SearchIndexDataReader`
- APIM → Cosmos DB: `CosmosDB Data Contributor` (role ID `00000000-0000-0000-0000-000000000002`)

---

## Manual Setup Steps (not in ARM template)

These must be done after the ARM template deploys, in this order:

### 1. Run `get-keys.sh` to populate `.env`
```bash
cd challenge-0
./get-keys.sh --resource-group <RG_NAME>
export $(cat ../.env | xargs)
```

Then **immediately fix the embedding model**:
```bash
# Edit .env: change text-embedding-ada-002 → text-embedding-3-large
sed -i 's/text-embedding-ada-002/text-embedding-3-large/' ../.env
```

### 2. Seed Cosmos DB, Blob Storage, APIM
```bash
PATH=/path/to/.venv/bin:$PATH ./seed-data.sh
```
This seeds:
- 9 Cosmos DB containers in `FactoryOpsDB`
- 5 wiki markdown files to blob storage (`kb-wiki/` container)
- Machine API + Maintenance API in APIM

### 3. Create MCP servers in APIM (Azure Portal)
In the Azure Portal → API Management → APIs, create two MCP servers:
- **Machine MCP**: route = `/get-machine-data/mcp` → backend is the Machine API
- **Maintenance MCP**: route = `/get-maintenance-data/mcp` → backend is the Maintenance API

Then set in `.env`:
```
MACHINE_MCP_SERVER_ENDPOINT=https://{apim}.azure-api.net/get-machine-data/mcp
MAINTENANCE_MCP_SERVER_ENDPOINT=https://{apim}.azure-api.net/get-maintenance-data/mcp
```

### 4. Create knowledge source and knowledge base (Challenge 1 Task 3)
Run `challenge-1/create_knowledge_base.ipynb` steps 1-3. **Use `text-embedding-3-large`** — not the default. The knowledge base name must match `KNOWLEDGE_BASE_NAME` in `fault_diagnosis_agent.py`.

If indexer reports 0 documents indexed, see `docs/08-IMPLEMENTATION-NOTES.md` §1 for the full delete-and-recreate procedure.

### 5. Assign additional permissions (Challenge 0 Task 7)
Ensure your identity has `Cognitive Services OpenAI Contributor` on the Azure OpenAI resource. Required for Challenge 4.

---

## Cosmos DB: Container Definitions

All in database `FactoryOpsDB`:

| Container | Partition key | Item count (seeded) |
|---|---|---|
| `Machines` | `/type` | 5 |
| `Thresholds` | `/machineType` | 13 |
| `Telemetry` | `/machineId` | 5+ |
| `KnowledgeBase` | `/machineType` | 10 |
| `PartsInventory` | `/category` | 16 |
| `Technicians` | `/department` | 6 |
| `WorkOrders` | `/status` | 5 (seeded) + more created by agents |
| `MaintenanceHistory` | `/machineId` | 12 |
| `MaintenanceWindows` | `/isAvailable` | 17 |

> ⚠️ The `WorkOrders` partition key is `/status`. New work orders created by agents add to this; status values used are: `open`, `in_progress`, `scheduled`, `Scheduled`, `PartsOrdered`, `Ready` (casing varies by agent — Python uses mixed case, standalone C# agent uses lowercase).

---

## Environment Variables: Complete Reference

```bash
# Resource group
RESOURCE_GROUP="hackuser26-rg"
AZURE_SUBSCRIPTION_ID="ba4faa0e-..."

# Azure AI Foundry / OpenAI
AZURE_AI_PROJECT_ENDPOINT="https://{aifoundry}.services.ai.azure.com/api/projects/{aiproject}"
AI_FOUNDRY_PROJECT_ENDPOINT="<same as above>"
AZURE_OPENAI_ENDPOINT="https://{aifoundry}.openai.azure.com/"
AZURE_OPENAI_KEY="<key>"
AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4.1"
MODEL_DEPLOYMENT_NAME="gpt-4.1"
AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1"
AZURE_AI_CHAT_MODEL_DEPLOYMENT_NAME="gpt-4o-mini"

# CRITICAL: must be text-embedding-3-large, NOT text-embedding-ada-002
EMBEDDING_MODEL_DEPLOYMENT_NAME="text-embedding-3-large"

# Cosmos DB
COSMOS_ENDPOINT="https://{cosmos}.documents.azure.com:443/"
COSMOS_KEY="<key>"
COSMOS_DATABASE_NAME="FactoryOpsDB"
COSMOS_DATABASE="FactoryOpsDB"   # alias used by some challenge-4 code

# Azure AI Search
AZURE_SEARCH_ENDPOINT="https://{search}.search.windows.net"
AZURE_SEARCH_API_KEY="<key>"

# APIM
APIM_GATEWAY_URL="https://{apim}.azure-api.net"
APIM_SUBSCRIPTION_KEY="<key>"

# MCP servers (set manually after creating MCP servers in APIM portal)
MACHINE_MCP_SERVER_ENDPOINT="https://{apim}.azure-api.net/get-machine-data/mcp"
MAINTENANCE_MCP_SERVER_ENDPOINT="https://{apim}.azure-api.net/get-maintenance-data/mcp"

# Application Insights
APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..."
APPLICATION_INSIGHTS_CONNECTION_STRING="<same>"

# Storage (for blob seeding)
AZURE_STORAGE_ACCOUNT_NAME="<name>"
AZURE_STORAGE_ACCOUNT_KEY="<key>"
AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"

# Project resource ID (for role assignments / SDK auth)
AZURE_AI_PROJECT_RESOURCE_ID="/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{aifoundry}/projects/{aiproject}"
```

---

## Python Dependencies

- Python 3.12 for challenges 1-3
- Python 3.13+ for challenge 4 (enforced in `pyproject.toml`)
- Always create a venv: `python3 -m venv .venv && source .venv/bin/activate`
- Always use `--pre` flag: `pip install --pre -r requirements.txt` (agent-framework packages are pre-release)
- Challenge 4 uses `uv` (not pip): `curl -LsSf https://astral.sh/uv/install.sh | sh`

---

## .NET Dependencies (Challenge 2 + 4)

- .NET 10 SDK
- **Pinned NuGet versions** — do not upgrade without testing:
  ```xml
  <PackageReference Include="Azure.AI.Projects" Version="1.2.0-beta.5" />
  <PackageReference Include="Microsoft.Agents.AI" Version="1.0.0-preview.260108.1" />
  <PackageReference Include="Microsoft.Azure.Cosmos" Version="3.56.0" />
  <PackageReference Include="Azure.Identity" Version="1.17.1" />
  ```
- Use `DefaultAzureCredential` + `az login` for auth — no key-based auth in .NET agents
- Never use `Azure.AI.Inference.ChatCompletionsClient` — use `Azure.AI.Projects.AIProjectClient`

---

## Aspire (Challenge 4)

- Install: `curl -fsSL https://aspire.dev/install.sh | bash -s`
- Version used: 13.1.2
- Run from: `challenge-4/agent-workflow/`
- Command: `export $(cat ../../.env | xargs) && aspire run`
- Dev cert trust fails on WSL — ignorable, services still work
- `VITE_API_URL` must use `ReferenceExpression.Create($"{endpoint}/")` — lambda-based `WithEnvironment` returns type name, not URL

---

## Agent System Prompts (verbatim openings)

**AnomalyClassificationAgent:**
> "You are a Anomaly Classification Agent evaluating machine anomalies for warning and critical threshold violations."

**FaultDiagnosisAgent:**
> "You are a Fault Diagnosis Agent evaluating the root cause of maintenance alerts."

**MaintenanceSchedulerAgent:**
> "You are a predictive maintenance expert specializing in industrial tire manufacturing equipment."

**PartsOrderingAgent:**
> "You are a parts ordering specialist for industrial tire manufacturing equipment."

**RepairPlannerAgent (Challenge 4 inline):**
> "You are a Repair Planner Agent for factory maintenance operations."

---

## MCP Tool Configuration (fault_diagnosis_agent.py)

The Foundry IQ MCP endpoint format:
```python
machine_wiki_mcp_endpoint = f"{search_endpoint}knowledgebases/{knowledge_base_name}/mcp?api-version=2025-11-01-preview"
```

Both MCPTool instances:
```python
MCPTool(server_label="machine-data",  server_url=machine_data_mcp_endpoint,
        require_approval="never", project_connection_id="machine-data-connection")

MCPTool(server_label="machine-wiki",  server_url=machine_wiki_mcp_endpoint,
        require_approval="never", project_connection_id="machine-wiki-connection")
```

Connection IDs must match names used when creating connections in Azure AI Foundry portal.

---

## Known Gaps / Things Not Captured

1. **Cosmos DB live document state** — Azure environment went offline before querying. Use seed data files in `challenge-0/data/` as source of truth. Documents created by agents during the session (work orders, maintenance schedules, parts orders) are lost.

2. **APIM API definitions** — The exact Swagger/OpenAPI specs for the Machine API and Maintenance API backends are not exported. The routes are reconstructed from `seed-data.sh` and agent source code.

3. **Knowledge base configuration** — The exact AI Search index schema (field names, vector config) for the Foundry IQ knowledge base is not exported. Recreate via the `create_knowledge_base.ipynb` notebook with `text-embedding-3-large`.

4. **Agent registration state** — The Azure AI Foundry portal stored agent definitions (AnomalyClassificationAgent v1, FaultDiagnosisAgent v1, MaintenanceSchedulerAgent v1, PartsOrderingAgent v1, RepairPlannerAgent v1) are lost when the environment shuts down. They are recreated automatically on first run of each agent script.

5. **ARM template embedding model bug** — The deployed `text-embedding-ada-002` is wrong for knowledge base creation. The ARM template was not fixed; the workaround is documented in `docs/08-IMPLEMENTATION-NOTES.md` §1.
