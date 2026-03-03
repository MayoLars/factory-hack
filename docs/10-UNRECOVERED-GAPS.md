# 10 — Unrecovered Gaps

Items that could not be extracted before the Azure environment shut down, with our best guess at what they contained and confidence level.

---

## What Was NOT Recoverable

### 1. APIM API Definitions — RECOVERED (false alarm)

Initially believed to be lost (APIM management plane returned 403). However, the full API definitions — both **Machine API** and **Maintenance API** — are embedded verbatim inside `challenge-0/seed-data.sh` as the `seed_apim_cosmos_mi.py` heredoc.

**The complete definitions include:**

**Machine API** (`path=/machine`):
- `GET /` → `list-machines` → queries `SELECT * FROM c` on `Machines` container
- `GET /{id}` → `get-machine` → queries by `c.id = @id`
- Auth: APIM Managed Identity to Cosmos (`authentication-managed-identity`)
- Cosmos API version: `2018-12-31`
- Cross-partition queries enabled

**Maintenance API** (`path=/maintenance`):
- `GET /` → `list-thresholds` → queries `SELECT * FROM c` on `Thresholds` container
- `GET /{machineType}` → `get-threshold` → queries by `c.machineType = @machineType`
- Same auth pattern as Machine API

Both APIs use APIM inbound policies that:
1. Get MSI token via `authentication-managed-identity` for `https://{cosmos}.documents.azure.com`
2. `send-request` POST to Cosmos REST API (`/dbs/FactoryOpsDB/colls/{collection}/docs`)
3. Set headers: `x-ms-documentdb-isquery: true`, `x-ms-documentdb-query-enablecrosspartition: true`
4. Return `Documents` array from Cosmos response, or first document for by-ID queries

**The MCP server layer** (`/get-machine-data/mcp`, `/get-maintenance-data/mcp`) is configured manually in the Azure portal on top of these APIs. That configuration is NOT in the seed script — see below.

---

### 2. MCP Server Configuration (Azure Portal) — PARTIAL GUESS

**What we know:**
- The MCP endpoints are APIM-hosted: `https://{apim}.azure-api.net/get-machine-data/mcp` and `.../get-maintenance-data/mcp`
- They proxy the Machine API and Maintenance API respectively
- They require `Ocp-Apim-Subscription-Key` header for auth
- The agent code registers them as project connections with names `machine-data-connection` and `maintenance-data-connection`

**What we're guessing:**
- The MCP servers are created via the Azure Portal → API Management → APIs → "+ Create" → "MCP Server"
- Each MCP server is linked to its corresponding API (`machine-api` / `maintenance-api`)
- The path suffix `/mcp` is the standard Aspire APIM MCP suffix

**Confidence: Medium.** The exact portal steps for creating MCP servers on top of APIM APIs are undocumented here. They were done manually during the hackathon. If the above guess is wrong, the alternative is that MCP is a separate API definition pointing to the Cosmos endpoint directly.

---

### 3. Azure AI Search Index Schema — PARTIAL GUESS

**What we know:**
- Index is created by `challenge-1/create_knowledge_base.ipynb`
- Embedding model must be `text-embedding-3-large` (NOT `text-embedding-ada-002`)
- The knowledge source is Azure Blob Storage container `machine-wiki` with 5 markdown files
- 10 documents indexed successfully after correct model setup
- The Foundry IQ MCP endpoint format: `{search_endpoint}knowledgebases/{kb_name}/mcp?api-version=2025-11-01-preview`

**What we're guessing:**
- The index schema is standard Foundry IQ schema: `id`, `content`, `content_vector` (float array, 3072 dims for text-embedding-3-large), `metadata_storage_name`, `metadata_storage_path`
- The knowledge base name used: `machine-wiki` (matches the connection name pattern)
- The indexer runs automatically and can be triggered manually

**Confidence: Medium-High.** The notebook creates this automatically — just run it with the right embedding model.

---

### 4. Agent Registrations in Azure AI Foundry — LOW RISK

**What we know:**
- 5 agents registered: `AnomalyClassificationAgent:1`, `FaultDiagnosisAgent:1`, `MaintenanceSchedulerAgent:1`, `PartsOrderingAgent:1`, `RepairPlannerAgent:1`
- All are re-registered automatically on first run of each agent script
- The Challenge 4 dotnet workflow calls `GetAIAgent("AnomalyClassificationAgent")` and `GetAIAgent("FaultDiagnosisAgent")` — these must exist in Foundry before running Challenge 4

**What we're guessing:** Nothing — this is fully recoverable by running the agents in order.

**Recovery procedure:**
1. Run `challenge-1/agents/anomaly_classification_agent.py` → registers `AnomalyClassificationAgent:1`
2. Run `challenge-1/agents/fault_diagnosis_agent.py` → registers `FaultDiagnosisAgent:1`
3. Run `challenge-3/agents/maintenance_scheduler_agent.py wo-2024-456` → registers `MaintenanceSchedulerAgent:1`
4. Run `challenge-3/agents/parts_ordering_agent.py wo-2024-456` → registers `PartsOrderingAgent:1`
5. Run `challenge-2/RepairPlanner/` dotnet app → registers `RepairPlannerAgent:1`

**Confidence: High.** All agent definitions are in source code.

---

### 5. Live Cosmos Documents Created During Session — LOST

Documents created by agents during the hackathon session that are NOT in the seed data:

| Container | Created by | Estimated count | Recovery |
|---|---|---|---|
| `WorkOrders` | Challenge 2 (batch), Challenge 4 | ~10 extra | Re-run agents |
| `WorkOrders` | Challenge 3 batch (status updates) | 5 status changes | Re-run batch |
| `MaintenanceSchedules` (if exists) | Challenge 3 scheduler | 5+ | Re-run batch |
| `PartsOrders` (if exists) | Challenge 3 ordering | 2+ | Re-run batch |

The baseline seed data in `challenge-0/data/` is the authoritative source. Re-seeding and re-running restores equivalent state.

**Confidence: N/A.** These are transient runtime documents, not needed for recreation.

---

### 6. `COSMOS_DATABASE` vs `COSMOS_DATABASE_NAME` — KNOWN QUIRK

Both variables exist in `.env` with identical values (`FactoryOpsDB`). This is because:
- Challenge 3 agents use `COSMOS_DATABASE_NAME`
- Challenge 4 `agents.py` falls back: `os.getenv("COSMOS_DATABASE_NAME") or os.getenv("COSMOS_DATABASE")`
- `seed-data.sh` appends `COSMOS_DATABASE="FactoryOpsDB"` as a second alias at the end

**When recreating:** Ensure `.env` has BOTH:
```
COSMOS_DATABASE_NAME="FactoryOpsDB"
COSMOS_DATABASE="FactoryOpsDB"
```

---

### 7. ARM Template Embedding Model Mismatch — KNOWN BUG (not a gap)

The ARM template in `challenge-0/infra/azuredeploy.json` deploys `text-embedding-ada-002`. The knowledge base creation requires `text-embedding-3-large`. These are two separate things:

- `text-embedding-ada-002` is deployed by the ARM template (and stays deployed — it's used nowhere else)
- `text-embedding-3-large` must be deployed **manually** in Azure AI Foundry after the ARM template runs
- `EMBEDDING_MODEL_DEPLOYMENT_NAME` in `.env` must be set to `text-embedding-3-large`

The ARM template was not fixed. This mismatch will bite anyone following only the ARM template + README.

---

## Summary Table

| Item | Status | Confidence | Recovery path |
|---|---|---|---|
| APIM Machine API definition | ✅ Recovered from seed-data.sh | High | Run seed-data.sh |
| APIM Maintenance API definition | ✅ Recovered from seed-data.sh | High | Run seed-data.sh |
| APIM MCP server config (portal steps) | ⚠️ Partial guess | Medium | Manual portal setup |
| Azure AI Search index schema | ⚠️ Partial guess | Medium-High | Run create_knowledge_base.ipynb |
| Agent registrations | ✅ Fully recoverable | High | Run agents in order |
| Live Cosmos documents | ❌ Lost | N/A | Re-run agents post-seed |
| `COSMOS_DATABASE` alias | ✅ Documented | High | Add to .env |
| ARM embedding model mismatch | ✅ Documented | High | Manual `text-embedding-3-large` deploy |
| All seed data | ✅ In repo | High | challenge-0/data/ |
| All source code | ✅ In repo | High | All challenges committed |
| All env var names | ✅ In docs | High | See 09-RECREATION-CHECKLIST.md |
