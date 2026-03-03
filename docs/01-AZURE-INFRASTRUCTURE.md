# Azure Infrastructure Reference

Complete specification of all Azure resources required, their configurations, SKUs, and interconnections.

## Resource Summary

| Resource Type | ARM Type | SKU | Purpose |
|--------------|----------|-----|---------|
| Cosmos DB | `Microsoft.DocumentDB/databaseAccounts` | Serverless | Data store for all factory data |
| AI Foundry Hub | `Microsoft.CognitiveServices/accounts` (kind: AIServices) | S0 | Azure AI Services hub |
| AI Foundry Project | `Microsoft.CognitiveServices/accounts/projects` | - | AI project under hub |
| Azure AI Search | `Microsoft.Search/searchServices` | Basic | Knowledge base indexing |
| API Management | `Microsoft.ApiManagement/service` | BasicV2 | MCP proxy to Cosmos DB |
| Storage Account | `Microsoft.Storage/storageAccounts` | Standard_LRS, StorageV2 | Blob storage for wiki docs |
| Application Insights | `Microsoft.Insights/components` | Web | Telemetry and observability |
| Log Analytics Workspace | `Microsoft.OperationalInsights/workspaces` | PerGB2018 | Log aggregation |
| Container Registry | `Microsoft.ContainerRegistry/registries` | Basic | Container image storage |
| Container App Environment | `Microsoft.App/managedEnvironments` | - | Container hosting environment |
| Container App | `Microsoft.App/containerApps` | 0.5 CPU, 1Gi RAM | Application hosting |
| Content Safety | `Microsoft.CognitiveServices/accounts` (kind: ContentSafety) | S0 | Content moderation |

## Naming Convention

All resources follow the pattern: `msagthack-<type>-<unique-suffix>`

Where `<unique-suffix>` = `uniqueString(resourceGroup().id, deployment().name)`

## Detailed Resource Specifications

### 1. Cosmos DB Account

```json
{
  "type": "Microsoft.DocumentDB/databaseAccounts",
  "kind": "GlobalDocumentDB",
  "identity": { "type": "SystemAssigned" },
  "properties": {
    "consistencyPolicy": { "defaultConsistencyLevel": "Session" },
    "databaseAccountOfferType": "Standard",
    "enableAutomaticFailover": false,
    "enableMultipleWriteLocations": false,
    "publicNetworkAccess": "Enabled",
    "disableLocalAuth": false,
    "capabilities": [{ "name": "EnableServerless" }]
  }
}
```

**Key characteristics:**
- **Serverless** capacity mode (pay-per-request, no provisioned throughput)
- Session consistency
- Single region, no failover
- Local auth enabled (key-based access)
- System-assigned managed identity (used for APIM integration)

### 2. AI Foundry Hub (Azure AI Services)

```json
{
  "type": "Microsoft.CognitiveServices/accounts",
  "apiVersion": "2025-04-01-preview",
  "kind": "AIServices",
  "sku": { "name": "S0" },
  "properties": {
    "allowProjectManagement": true,
    "customSubDomainName": "<unique-name>",
    "disableLocalAuth": false,
    "publicNetworkAccess": "Enabled"
  }
}
```

**Model Deployments on the Hub:**

| Name | Model | Version | SKU | Capacity (TPM) |
|------|-------|---------|-----|----------------|
| `gpt-4o-mini` | gpt-4o-mini | 2024-07-18 | GlobalStandard | 50K |
| `gpt-4.1` | gpt-4.1 | 2025-04-14 | GlobalStandard | 50K |
| `text-embedding-ada-002` | text-embedding-ada-002 | 2 | Standard | 50K |

**Connections configured on the Hub:**
- Azure AI Search connection (type: `CognitiveSearch`, auth: API key, shared to all projects)
- Application Insights connection (type: `AppInsights`, auth: API key)

### 3. AI Foundry Project

```json
{
  "type": "Microsoft.CognitiveServices/accounts/projects",
  "apiVersion": "2025-04-01-preview",
  "identity": { "type": "SystemAssigned" }
}
```

**Endpoint format:**
```
https://<hub-name>.services.ai.azure.com/api/projects/<project-name>
```

**Resource ID format:**
```
/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<hub>/projects/<project>
```

### 4. Azure AI Search

```json
{
  "type": "Microsoft.Search/searchServices",
  "identity": { "type": "SystemAssigned" },
  "sku": { "name": "basic" },
  "properties": {
    "hostingMode": "default",
    "replicaCount": 1,
    "partitionCount": 1,
    "publicNetworkAccess": "enabled",
    "disableLocalAuth": false,
    "authOptions": {
      "aadOrApiKey": {
        "aadAuthFailureMode": "http401WithBearerChallenge"
      }
    }
  }
}
```

### 5. API Management

```json
{
  "type": "Microsoft.ApiManagement/service",
  "identity": { "type": "SystemAssigned" },
  "sku": { "name": "BasicV2", "capacity": 1 },
  "properties": {
    "publisherEmail": "admin@contoso.com",
    "publisherName": "Contoso"
  }
}
```

**APIs configured by seed script:**

| API | Path | Operations |
|-----|------|-----------|
| Machine API | `/machine` | `GET /` (list all), `GET /{id}` (by ID) |
| Maintenance API | `/maintenance` | `GET /` (list all), `GET /{machineType}` (by type) |

Each operation uses an inline APIM policy that:
1. Gets current UTC timestamp
2. Acquires Managed Identity token for Cosmos DB resource
3. POSTs a SQL query to Cosmos DB REST API
4. Returns the Documents array from the response

**MCP Endpoints (derived from APIM):**
```
Machine MCP:     {APIM_GATEWAY_URL}/get-machine-data/mcp
Maintenance MCP: {APIM_GATEWAY_URL}/get-maintenance-data/mcp
```

### 6. Storage Account

```json
{
  "type": "Microsoft.Storage/storageAccounts",
  "sku": { "name": "Standard_LRS" },
  "kind": "StorageV2",
  "properties": {
    "allowBlobPublicAccess": true,
    "publicNetworkAccess": "Enabled",
    "allowSharedKeyAccess": true
  }
}
```

**Blob container:** `machine-wiki` (holds 5 markdown knowledge base files)

### 7. Container App

```json
{
  "properties": {
    "configuration": {
      "ingress": {
        "external": true,
        "targetPort": 8080,
        "allowInsecure": false
      }
    },
    "template": {
      "containers": [{
        "name": "main",
        "resources": { "cpu": 0.5, "memory": "1Gi" }
      }],
      "scale": {
        "minReplicas": 1,
        "maxReplicas": 10,
        "rules": [{
          "name": "http-rule",
          "http": { "metadata": { "concurrentRequests": "30" } }
        }]
      }
    }
  }
}
```

## Role Assignments (RBAC)

These are critical for service-to-service communication:

| Principal | Target | Role | Purpose |
|-----------|--------|------|---------|
| Search Service (MI) | AI Foundry Hub | Cognitive Services User | Search accesses AI models |
| AI Foundry Hub (MI) | Search Service | Search Service Contributor | Hub manages search indexes |
| AI Foundry Project (MI) | Search Service | Search Service Contributor | Project manages search indexes |
| AI Foundry Project (MI) | Search Service | Search Index Data Reader | Project reads search data |
| APIM (MI) | Cosmos DB | Cosmos DB Data Contributor | APIM queries Cosmos via MI |

**Cosmos DB role assignment** uses built-in role definition ID: `00000000-0000-0000-0000-000000000002` (Cosmos DB Built-in Data Contributor)

## Enterprise Production Recommendations

### Security Hardening

1. **Disable local auth** on Cosmos DB, AI Foundry, Search - use Managed Identity only
2. **Disable public access** on all services, use Private Endpoints + VNet
3. **Use Key Vault** for all secrets instead of `.env` files
4. **Enable RBAC** instead of key-based auth for APIM -> Cosmos DB
5. **Restrict APIM** to internal VNet with Application Gateway frontend

### Scaling

1. **Cosmos DB**: Switch from Serverless to Provisioned (autoscale) for production workloads
2. **AI Search**: Consider Standard SKU for higher query volume and more indexes
3. **APIM**: Use Standard or Premium SKU for production-grade rate limiting and policies
4. **Container Apps**: Configure appropriate min/max replicas for each service

### Monitoring

1. Enable diagnostic settings on all resources -> Log Analytics
2. Configure Application Insights sampling rates for production
3. Set up alerts on: Cosmos DB RU consumption, APIM errors, AI model latency
4. Use OpenTelemetry traces (already instrumented in the agents)

### Networking

```
                    VNet
                    ├── Subnet: Private Endpoints
                    │   ├── Cosmos DB PE
                    │   ├── AI Search PE
                    │   ├── Storage PE
                    │   └── AI Foundry PE
                    ├── Subnet: Container Apps
                    │   └── Container App Environment
                    └── Subnet: APIM
                        └── API Management (Internal)
```
