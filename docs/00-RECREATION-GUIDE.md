# Factory Predictive Maintenance System - Complete Recreation Guide

This document provides a step-by-step guide to recreate the entire multi-agent predictive maintenance system from scratch in a new Azure environment.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Quick Start Checklist](#quick-start-checklist)
3. [Phase 1: Azure Infrastructure](#phase-1-azure-infrastructure)
4. [Phase 2: Data Seeding](#phase-2-data-seeding)
5. [Phase 3: Knowledge Base Setup](#phase-3-knowledge-base-setup)
6. [Phase 4: Agent Development](#phase-4-agent-development)
7. [Phase 5: Orchestration](#phase-5-orchestration)
8. [Troubleshooting](#troubleshooting)

## Prerequisites

### Azure Subscription Requirements

- Azure subscription with permission to create resources
- Contributor role on the target resource group
- Ability to deploy: Cosmos DB, AI Foundry (Cognitive Services), Azure AI Search, API Management, Storage Account, Container Registry, Application Insights, Log Analytics, Container Apps

### Development Environment

| Tool | Version | Notes |
|------|---------|-------|
| Python | >= 3.13 | Required for Challenge 4 FastAPI app; 3.11+ for others |
| .NET SDK | 10.0 | Target framework is `net10.0` |
| Node.js | LTS (22+) | For the React frontend |
| Azure CLI | Latest | For resource management and `az login` |
| uv | Latest | Python package manager (optional, recommended for Ch4) |
| .NET Aspire | 13.1.0 | For multi-service orchestration |

### Install Development Dependencies

```bash
# Python virtual environment
python3 -m venv .venv
source .venv/bin/activate
pip install --pre -r requirements.txt

# .NET restore
dotnet restore factory-hack.sln

# Frontend
cd challenge-4/agent-workflow/frontend && npm install
```

## Quick Start Checklist

- [ ] Deploy Azure infrastructure (ARM template)
- [ ] Run `get-keys.sh` to generate `.env`
- [ ] Run `seed-data.sh` to populate Cosmos DB + Blob + APIM
- [ ] Create Foundry IQ knowledge base (Jupyter notebook)
- [ ] Create MCP project connections in AI Foundry portal
- [ ] Run Challenge 1 agents to register them in Foundry
- [ ] Build Challenge 2 Repair Planner
- [ ] Run Challenge 3 agents to register them
- [ ] Wire everything up in Challenge 4 orchestration

---

## Phase 1: Azure Infrastructure

### Option A: ARM Template Deployment

Deploy the included ARM template to create all resources at once:

```bash
# Create resource group
az group create --name <RG_NAME> --location swedencentral

# Deploy ARM template
az deployment group create \
  --resource-group <RG_NAME> \
  --template-file challenge-0/infra/azuredeploy.json \
  --parameters location=swedencentral searchServiceSku=basic
```

**Allowed regions:** `swedencentral`, `francecentral`, `germanywestcentral`

### Option B: Manual Resource Creation

See `docs/01-AZURE-INFRASTRUCTURE.md` for individual resource specifications.

### Extract Keys

After deployment, extract all keys into `.env`:

```bash
cd challenge-0
./get-keys.sh --resource-group <RG_NAME>
export $(cat ../.env | xargs)
```

### Post-Deployment: Model Deployments

The ARM template deploys three model deployments on the AI Foundry hub:

| Deployment Name | Model | SKU | Capacity |
|----------------|-------|-----|----------|
| `gpt-4o-mini` | gpt-4o-mini (2024-07-18) | GlobalStandard | 50 |
| `gpt-4.1` | gpt-4.1 (2025-04-14) | GlobalStandard | 50 |
| `text-embedding-ada-002` | text-embedding-ada-002 (v2) | Standard | 50 |

> **Note:** The actual deployed embedding model may be `text-embedding-3-large` depending on region availability. Update `EMBEDDING_MODEL_DEPLOYMENT_NAME` in `.env` accordingly.

### Post-Deployment: Role Assignments

The ARM template configures these role assignments automatically:

1. **Search Service** -> AI Foundry Hub: `Cognitive Services User` role
2. **AI Foundry Hub** -> Search Service: `Search Service Contributor` role
3. **AI Foundry Project** -> Search Service: `Search Service Contributor` + `Search Index Data Reader` roles
4. **APIM Managed Identity** -> Cosmos DB: `Cosmos DB Data Contributor` role (for MCP proxy)
5. **AI Foundry Hub** -> Search Service: Connection (`CognitiveSearch` type, API key auth)

---

## Phase 2: Data Seeding

### Run the Seed Script

```bash
cd challenge-0
./seed-data.sh
```

This does three things:

#### 1. Cosmos DB Seeding

Creates database `FactoryOpsDB` with 9 containers and seeds them from JSON files:

| Container | Partition Key | Data File | Records |
|-----------|--------------|-----------|---------|
| Machines | `/type` | `data/machines.json` | 5 |
| Thresholds | `/machineType` | `data/thresholds.json` | 5 |
| Telemetry | `/machineId` (TTL: 30d) | `data/telemetry-samples.json` | varies |
| KnowledgeBase | `/machineType` | `data/knowledge-base.json` | varies |
| PartsInventory | `/category` | `data/parts-inventory.json` | varies |
| Technicians | `/department` | `data/technicians.json` | varies |
| WorkOrders | `/status` | `data/work-orders.json` | varies |
| MaintenanceHistory | `/machineId` | `data/maintenance-history.json` | varies |
| MaintenanceWindows | `/isAvailable` | `data/maintenance-windows.json` | varies |

#### 2. Blob Storage Seeding

Uploads knowledge base wiki markdown files to blob container `machine-wiki`:
- `banbury_mixer.md`
- `tire_building_machine.md`
- `tire_curing_press.md`
- `tire_extruder.md`
- `tire_uniformity_machine.md`

#### 3. APIM Configuration

Creates two APIs with Cosmos DB proxy policies (via APIM Managed Identity):

**Machine API** (path: `/machine`):
- `GET /` - List all machines (queries `Machines` container)
- `GET /{id}` - Get machine by ID

**Maintenance API** (path: `/maintenance`):
- `GET /` - List all thresholds (queries `Thresholds` container)
- `GET /{machineType}` - Get threshold by machine type

Each operation uses an APIM policy that:
1. Acquires a Managed Identity token for Cosmos DB
2. Sends a POST to Cosmos DB REST API with SQL query
3. Returns the `Documents` array from the response

---

## Phase 3: Knowledge Base Setup

### Create Foundry IQ Knowledge Base

Run the Jupyter notebook to set up the AI Search-backed knowledge base:

```bash
cd challenge-1
jupyter notebook create_knowledge_base.ipynb
```

This creates:
1. An Azure AI Search index named `machine-kb`
2. Indexes the wiki markdown content from blob storage
3. The knowledge base is accessible via MCP endpoint:
   ```
   https://<search-service>.search.windows.net/knowledgebases/machine-kb/mcp?api-version=2025-11-01-preview
   ```

### Create MCP Project Connections

In the Azure AI Foundry portal, create two project connections:

1. **`machine-data-connection`**: Points to APIM Machine API MCP endpoint
   - URL: `{APIM_GATEWAY_URL}/get-machine-data/mcp`
   - Auth: API key (APIM subscription key)

2. **`machine-wiki-connection`**: Points to Foundry IQ knowledge base MCP endpoint
   - URL: `{SEARCH_SERVICE_ENDPOINT}knowledgebases/machine-kb/mcp?api-version=2025-11-01-preview`
   - Auth: API key (Search admin key)

These are referenced by the Fault Diagnosis Agent's MCPTool definitions.

---

## Phase 4: Agent Development

See `docs/03-AGENT-IMPLEMENTATIONS.md` for complete agent code and prompt details.

### Challenge 1: Python Agents

```bash
cd challenge-1

# Register and test Anomaly Classification Agent
python agents/anomaly_classification_agent.py

# Register and test Fault Diagnosis Agent
python agents/fault_diagnosis_agent.py
```

### Challenge 2: C# Repair Planner

```bash
cd challenge-2/RepairPlanner
dotnet build
dotnet run
```

### Challenge 3: Scheduler + Parts Ordering

```bash
cd challenge-3

# Run batch processing for all 5 work orders
python run-batch.py
```

---

## Phase 5: Orchestration (Challenge 4)

### Architecture

Challenge 4 wires everything together with three services:

```
[React Frontend :3000] → [.NET Workflow API :5231] → [Python FastAPI :8000]
                               ↓                          ↓
                    Azure AI Foundry (Agents)    A2A Protocol (Scheduler/Parts)
                    Local RepairPlanner           Cosmos DB
```

### Running with Aspire

```bash
cd challenge-4/agent-workflow
dotnet run apphost.cs
```

This starts all three services. The Aspire dashboard shows health and telemetry.

### Running Individually

```bash
# Terminal 1: Python FastAPI
cd challenge-4/agent-workflow/app
uv run uvicorn main:app --host 0.0.0.0 --port 8000

# Terminal 2: .NET Workflow
cd challenge-4/agent-workflow/dotnetworkflow
dotnet run

# Terminal 3: Frontend
cd challenge-4/agent-workflow/frontend
npm run dev
```

### Testing the Pipeline

```bash
curl -X POST http://localhost:5231/api/analyze_machine \
  -H "Content-Type: application/json" \
  -d '{
    "machine_id": "machine-001",
    "telemetry": {"curing_temperature": 179.2, "cycle_time": 14.5}
  }'
```

---

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| `pip install` fails | Use `pip install --pre -r requirements.txt` (pre-release packages) |
| MCP tools not working | Verify APIM subscription key and project connections in Foundry portal |
| Agent not found in Foundry | Run the agent script first to register via `create_version()` |
| Cosmos DB 404 errors | Run `seed-data.sh` to create containers and seed data |
| WorkOrder update fails | WorkOrders partition key is `/status` - must delete+reinsert on status change |
| .NET build fails | Ensure .NET 10.0 SDK and exact NuGet versions from `.csproj` |
| Python 3.13 required | Challenge 4 FastAPI app requires Python >= 3.13 |
| TextOnlyAgentExecutor | SDK workaround - MCP tool history causes deserialization errors between agents |
| Embedding model mismatch | Check if `text-embedding-3-large` was deployed instead of `ada-002` |

### Key Environment Variables to Verify

```bash
# These must all be set for the full pipeline to work:
echo $AZURE_AI_PROJECT_ENDPOINT    # Foundry project API endpoint
echo $MODEL_DEPLOYMENT_NAME        # Should be "gpt-4.1"
echo $COSMOS_ENDPOINT              # Cosmos DB endpoint
echo $COSMOS_KEY                   # Cosmos DB key
echo $COSMOS_DATABASE_NAME         # Should be "FactoryOpsDB"
echo $APIM_GATEWAY_URL             # APIM gateway URL
echo $APIM_SUBSCRIPTION_KEY        # APIM subscription key
echo $SEARCH_SERVICE_ENDPOINT      # Azure AI Search endpoint
echo $AZURE_SEARCH_API_KEY         # Search admin key
echo $AZURE_OPENAI_ENDPOINT        # OpenAI-compatible endpoint
echo $AZURE_OPENAI_KEY             # OpenAI key
```
