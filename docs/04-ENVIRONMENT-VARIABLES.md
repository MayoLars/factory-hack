# Environment Variables Reference

Complete list of all environment variables used across the system, which components use them, and how they're generated.

## How Environment Variables Are Generated

1. Deploy Azure infrastructure (ARM template)
2. Run `challenge-0/get-keys.sh --resource-group <RG_NAME>`
3. Script writes all variables to `<repo-root>/.env`
4. Load with: `export $(cat .env | xargs)`

## Complete Variable List

### Azure Resource Group

| Variable | Example | Used By |
|----------|---------|---------|
| `RESOURCE_GROUP` | `hackuser26-rg` | Setup scripts |
| `AZURE_SUBSCRIPTION_ID` | `ba4faa0e-...` | Connection ID construction |

### Azure Storage

| Variable | Example | Used By |
|----------|---------|---------|
| `AZURE_STORAGE_ACCOUNT_NAME` | `msagthacksa2bynis23zmxny` | Blob seeding |
| `AZURE_STORAGE_ACCOUNT_KEY` | `GkwEwR...` | Blob seeding |
| `AZURE_STORAGE_CONNECTION_STRING` | `DefaultEndpointsProtocol=https;...` | Blob seeding |

### Azure AI Search

| Variable | Example | Used By |
|----------|---------|---------|
| `SEARCH_SERVICE_NAME` | `msagthack-search-2bynis23zmxny` | Reference only |
| `SEARCH_SERVICE_ENDPOINT` | `https://msagthack-search-2bynis23zmxny.search.windows.net` | Fault Diagnosis MCP, Knowledge Base |
| `SEARCH_ADMIN_KEY` | `5kmcUs9U...` | Search admin operations |
| `AZURE_SEARCH_ENDPOINT` | (same as SEARCH_SERVICE_ENDPOINT) | Knowledge base creation |
| `AZURE_SEARCH_API_KEY` | (same as SEARCH_ADMIN_KEY) | Knowledge base creation |

### Azure AI Foundry

| Variable | Example | Used By |
|----------|---------|---------|
| `AI_FOUNDRY_HUB_NAME` | `msagthack-aifoundry-2bynis23zmxny` | Reference, endpoint construction |
| `AI_FOUNDRY_PROJECT_NAME` | `msagthack-aiproject-2bynis23zmxny` | Reference only |
| `AI_FOUNDRY_ENDPOINT` | `https://<hub>.cognitiveservices.azure.com/` | Agent key-based auth |
| `AI_FOUNDRY_KEY` | `Bc1FFf...` | Agent key-based auth |
| `AI_FOUNDRY_PROJECT_ENDPOINT` | `https://<hub>.services.ai.azure.com/api/projects/<project>` | All agents (same as AZURE_AI_PROJECT_ENDPOINT) |
| `AZURE_AI_PROJECT_ENDPOINT` | (same as above) | **Primary** - used by all agent code |
| `AZURE_AI_PROJECT_RESOURCE_ID` | `/subscriptions/.../accounts/<hub>/projects/<project>` | Foundry resource references |
| `AZURE_AI_CONNECTION_ID` | `/subscriptions/.../accounts/<hub>/connections/<hub>-aisearch` | Search connection in Foundry |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | `gpt-4.1` | Model deployment reference |

### Model Deployments

| Variable | Example | Used By |
|----------|---------|---------|
| `MODEL_DEPLOYMENT_NAME` | `gpt-4.1` | **Primary** - All agents use this |
| `EMBEDDING_MODEL_DEPLOYMENT_NAME` | `text-embedding-3-large` | Knowledge base creation |
| `AZURE_AI_CHAT_MODEL_DEPLOYMENT_NAME` | `gpt-4o-mini` | Challenge 2 chat (legacy) |

### Azure OpenAI (Backward Compatibility)

| Variable | Example | Used By |
|----------|---------|---------|
| `AZURE_OPENAI_SERVICE_NAME` | `msagthack-aifoundry-2bynis23zmxny` | Reference |
| `AZURE_OPENAI_ENDPOINT` | `https://<hub>.openai.azure.com/` | Challenge 4 RepairPlanner |
| `AZURE_OPENAI_KEY` | (same as AI_FOUNDRY_KEY) | Challenge 4 RepairPlanner |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | `gpt-4.1` | Challenge 4 RepairPlanner |

### Cosmos DB

| Variable | Example | Used By |
|----------|---------|---------|
| `COSMOS_NAME` | `msagthack-cosmos-2bynis23zmxny` | Reference only |
| `COSMOS_ENDPOINT` | `https://<name>.documents.azure.com:443/` | **All agents** |
| `COSMOS_KEY` | `L2WJtk...` | **All agents** |
| `COSMOS_DATABASE_NAME` | `FactoryOpsDB` | **All agents** |
| `COSMOS_DATABASE` | `FactoryOpsDB` | Alias (some scripts) |
| `COSMOS_CONNECTION_STRING` | `AccountEndpoint=...;AccountKey=...;` | Reference only |

### API Management

| Variable | Example | Used By |
|----------|---------|---------|
| `APIM_NAME` | `msagthack-apim-2bynis23zmxny` | APIM seeding script |
| `APIM_GATEWAY_URL` | `https://<name>.azure-api.net` | MCP endpoint construction |
| `APIM_SUBSCRIPTION_KEY` | `42a245b0...` | MCP tool auth headers |

### MCP Server Endpoints (Derived)

| Variable | Example | Used By |
|----------|---------|---------|
| `MACHINE_MCP_SERVER_ENDPOINT` | `{APIM_GATEWAY_URL}/get-machine-data/mcp` | Fault Diagnosis Agent |
| `MAINTENANCE_MCP_SERVER_ENDPOINT` | `{APIM_GATEWAY_URL}/get-maintenance-data/mcp` | Anomaly Agent (MCP variant) |

### Container Registry

| Variable | Example | Used By |
|----------|---------|---------|
| `ACR_NAME` | `msagthackcr2bynis23zmxny` | Container publishing |
| `ACR_USERNAME` | (same as ACR_NAME) | Docker login |
| `ACR_PASSWORD` | `1jZIYU...` | Docker login |
| `ACR_LOGIN_SERVER` | `msagthackcr2bynis23zmxny.azurecr.io` | Docker push |

### Application Insights / Telemetry

| Variable | Example | Used By |
|----------|---------|---------|
| `APPLICATION_INSIGHTS_INSTRUMENTATION_KEY` | `dbdcdf81-...` | Legacy reference |
| `APPLICATION_INSIGHTS_CONNECTION_STRING` | `InstrumentationKey=...;IngestionEndpoint=...` | OpenTelemetry exporters |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | (same as above) | Python SDK auto-detection |

### Challenge 4 Specific (Set by Aspire or manually)

| Variable | Example | Used By |
|----------|---------|---------|
| `MAINTENANCE_SCHEDULER_AGENT_URL` | `https://localhost:8000/maintenance-scheduler` | .NET workflow -> Python A2A |
| `PARTS_ORDERING_AGENT_URL` | `https://localhost:8000/parts-ordering` | .NET workflow -> Python A2A |
| `REPAIR_PLANNER_AGENT_URL` | (optional, for A2A variant) | Python workflow |
| `VITE_API_URL` | `http://localhost:5231` | Frontend -> .NET workflow |

## Endpoint Format Reference

### AI Foundry Project Endpoint
```
https://<hub-name>.services.ai.azure.com/api/projects/<project-name>
```
This is the **API** endpoint (not the portal URL). The `get-keys.sh` script constructs this from the hub name and project name.

### Azure OpenAI Endpoint
```
https://<hub-name>.openai.azure.com/
```
Used for direct OpenAI-compatible API calls (Challenge 4 RepairPlanner).

### AI Foundry Cognitive Services Endpoint
```
https://<hub-name>.cognitiveservices.azure.com/
```
Used for key-based authentication to AI services.

### Cosmos DB Endpoint
```
https://<account-name>.documents.azure.com:443/
```

### APIM Gateway
```
https://<apim-name>.azure-api.net
```

### Search Service Endpoint
```
https://<search-name>.search.windows.net
```

### Foundry IQ Knowledge Base MCP Endpoint
```
https://<search-name>.search.windows.net/knowledgebases/<kb-name>/mcp?api-version=2025-11-01-preview
```

## Minimal .env Template

For recreating the environment, these are the essential variables:

```bash
# Core AI
AZURE_AI_PROJECT_ENDPOINT="https://<hub>.services.ai.azure.com/api/projects/<project>"
MODEL_DEPLOYMENT_NAME="gpt-4.1"
EMBEDDING_MODEL_DEPLOYMENT_NAME="text-embedding-3-large"

# Cosmos DB
COSMOS_ENDPOINT="https://<account>.documents.azure.com:443/"
COSMOS_KEY="<primary-key>"
COSMOS_DATABASE_NAME="FactoryOpsDB"

# Azure OpenAI (for C# agents)
AZURE_OPENAI_ENDPOINT="https://<hub>.openai.azure.com/"
AZURE_OPENAI_KEY="<key>"

# APIM (for MCP tools)
APIM_GATEWAY_URL="https://<apim>.azure-api.net"
APIM_SUBSCRIPTION_KEY="<subscription-key>"

# Search (for Knowledge Base)
SEARCH_SERVICE_ENDPOINT="https://<search>.search.windows.net"
AZURE_SEARCH_API_KEY="<admin-key>"

# MCP Endpoints
MACHINE_MCP_SERVER_ENDPOINT="${APIM_GATEWAY_URL}/get-machine-data/mcp"
MAINTENANCE_MCP_SERVER_ENDPOINT="${APIM_GATEWAY_URL}/get-maintenance-data/mcp"

# Telemetry (optional)
APPLICATION_INSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..."
APPLICATIONINSIGHTS_CONNECTION_STRING="${APPLICATION_INSIGHTS_CONNECTION_STRING}"

# Storage (for wiki upload only)
AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
```
