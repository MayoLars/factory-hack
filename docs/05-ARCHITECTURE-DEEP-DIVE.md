# Architecture Deep Dive

Detailed technical architecture of the multi-agent system, communication patterns, and service topology.

## System Architecture

```
                                ┌─────────────────────────────────────┐
                                │         Azure AI Foundry             │
                                │  ┌───────────────────────────────┐  │
                                │  │ Agent Service (Hosted)         │  │
                                │  │  - AnomalyClassificationAgent │  │
                                │  │  - FaultDiagnosisAgent        │  │
                                │  │  - MaintenanceSchedulerAgent  │  │
                                │  │    (registered, not hosted)   │  │
                                │  │  - PartsOrderingAgent         │  │
                                │  │    (registered, not hosted)   │  │
                                │  └───────────────────────────────┘  │
                                │  ┌───────────────────────────────┐  │
                                │  │ Model Deployments              │  │
                                │  │  - gpt-4.1 (primary)          │  │
                                │  │  - gpt-4o-mini (legacy)       │  │
                                │  │  - text-embedding-3-large     │  │
                                │  └───────────────────────────────┘  │
                                │  ┌───────────────────────────────┐  │
                                │  │ Project Connections            │  │
                                │  │  - machine-data-connection    │  │
                                │  │  - machine-wiki-connection    │  │
                                │  │  - AI Search connection       │  │
                                │  │  - App Insights connection    │  │
                                │  └───────────────────────────────┘  │
                                └──────────────┬──────────────────────┘
                                               │
               ┌───────────────────────────────┼───────────────────────────────┐
               │                               │                               │
    ┌──────────▼──────────┐       ┌───────────▼────────────┐     ┌────────────▼───────────┐
    │   API Management    │       │   Azure AI Search       │     │   Azure Cosmos DB      │
    │   (APIM)            │       │                         │     │   (FactoryOpsDB)       │
    │                     │       │  Index: machine-kb      │     │                        │
    │  /machine/* ─────────────────►  (Foundry IQ KB)      │     │  9 seeded containers   │
    │  /maintenance/* ──┐ │       │                         │     │  3 runtime containers  │
    │                   │ │       └─────────────────────────┘     │                        │
    │  MCP Proxy:       │ │                                       │  Machines, Thresholds  │
    │  MI→Cosmos REST   ├─────────────────────────────────────────►  Telemetry, WorkOrders │
    │                   │ │                                       │  PartsInventory, etc.  │
    └───────────────────┘ │                                       └────────────────────────┘
                                                                          ▲
               ┌──────────────────────────────────────────────────────────┤
               │                                                          │
    ┌──────────▼──────────────────────────────────────────────────────────┤
    │                    Challenge 4 Service Topology                      │
    │                                                                      │
    │  ┌────────────────┐     ┌─────────────────────┐    ┌──────────────┐ │
    │  │ React Frontend │────►│ .NET Workflow API    │───►│ Python       │ │
    │  │ (Vite :3000)   │    │ (ASP.NET :5231)      │   │ FastAPI      │ │
    │  │                │    │                       │   │ (:8000)      │ │
    │  │ POST /api/     │    │ POST /api/            │   │              │ │
    │  │ analyze_machine│    │ analyze_machine       │   │ /maintenance-│ │
    │  │                │    │                       │   │  scheduler   │ │
    │  └────────────────┘    │ Pipeline:             │   │  (A2A)      │ │
    │                        │ 1. Anomaly (Foundry)  │   │              │ │
    │                        │ 2. Fault (Foundry)    │   │ /parts-      │ │
    │                        │ 3. Repair (Local)     │   │  ordering    │ │
    │                        │ 4. Scheduler (A2A)────┼──►│  (A2A)      │ │
    │                        │ 5. Parts (A2A)────────┼──►│              │ │
    │                        └─────────────────────┘    └──────────────┘ │
    └──────────────────────────────────────────────────────────────────────┘
```

## Communication Patterns

### Pattern 1: Function Tools (Challenge 1 - Direct Cosmos)

```
User Prompt → AzureAIClient → GPT-4.1 → Tool Call → Python Function → Cosmos DB SDK → Response
```

The agent framework:
1. Sends the prompt + tool definitions to the model
2. Model decides which tool to call with what parameters
3. Framework executes the Python function locally
4. Function result goes back to the model
5. Model generates final response

### Pattern 2: MCP Tools (Challenge 1 - APIM Proxy)

```
User Prompt → AIProjectClient → Foundry Agent Service → MCP Tool Call → APIM → Cosmos REST API
                                                       → MCP Tool Call → AI Search (Foundry IQ)
```

The MCP (Model Context Protocol) pattern:
1. Agent is registered in Foundry with MCPTool definitions
2. Each MCPTool specifies a server URL and project connection
3. Foundry Agent Service handles MCP protocol server-side
4. APIM receives MCP calls and translates to Cosmos DB REST queries via Managed Identity

### Pattern 3: Foundry Agents SDK (Challenge 2 - C#)

```
User Prompt → AIProjectClient.Agents.CreateVersion → GetAIAgent → RunAsync → Text Response
```

1. Register agent definition in Foundry (prompt + model)
2. Get agent reference by name
3. Run agent with user prompt
4. Parse text response (JSON) locally

### Pattern 4: A2A Protocol (Challenge 4 - Cross-Service)

```
.NET Workflow → HTTP POST → Python FastAPI → A2A Starlette App → AgentExecutor → Agent Logic
                                                                        ↓
                                                                 Response Message
                                                                        ↓
                                                              ← HTTP Response ←
```

A2A (Agent-to-Agent) protocol:
1. Python agents are wrapped as A2A Starlette applications
2. Each has an `AgentCard` (name, URL, capabilities, skills)
3. Card is discoverable at `/.well-known/agent-card.json`
4. .NET resolves the card via `A2ACardResolver`
5. Messages are exchanged as `Message` objects with `TextPart` content
6. All A2A responses are text-only (no structured tool calls)

### Pattern 5: Workflow Pipeline (Challenge 4)

```
WorkflowBuilder → TextOnlyAgentExecutor chain → Sequential execution → Aggregated results
```

The `TextOnlyAgentExecutor` is a critical wrapper that:
1. Extracts only the text content from each agent's response
2. Strips MCP tool call history that causes deserialization errors
3. Passes clean text to the next agent in the pipeline
4. Collects `AgentStepResult` objects for the frontend

## Data Flow Through Pipeline

### Input
```json
{
  "machine_id": "machine-001",
  "telemetry": { "curing_temperature": 179.2, "cycle_time": 14.5 }
}
```

### Step 1: Anomaly Classification
- **Input:** Telemetry readings
- **Tools called:** `get_machine_data("machine-001")`, `get_thresholds("tire_curing_press")`
- **Output:** `{ "status": "high", "alerts": [...], "summary": { "critical": 0, "warning": 2 } }`

### Step 2: Fault Diagnosis
- **Input:** Anomaly classification result (text)
- **MCP calls:** machine-data (APIM), machine-wiki (Foundry IQ)
- **Output:** `{ "MachineId": "machine-001", "FaultType": "curing_temperature_excessive", "Severity": "High", ... }`

### Step 3: Repair Planning
- **Input:** Fault diagnosis result (text)
- **Tool calls:** `GetAvailableTechnicians(skills)`, `GetAvailableParts(partNumbers)`, `CreateWorkOrder(workOrder)`
- **Output:** WorkOrder JSON (also saved to Cosmos DB)

### Step 4: Maintenance Scheduling
- **Input:** Repair plan / work order text (A2A message)
- **Data loaded:** Work order, maintenance history, maintenance windows from Cosmos DB
- **Output:** MaintenanceSchedule (saved to Cosmos DB)

### Step 5: Parts Ordering
- **Input:** Scheduling result text (A2A message)
- **Data loaded:** Work order, parts inventory, suppliers from Cosmos DB
- **Output:** PartsOrder (saved to Cosmos DB), work order status updated

## Aspire Orchestration

The `apphost.cs` is a C# script (not a `.csproj`) using Aspire 13.1.0:

```csharp
#:sdk Aspire.AppHost.Sdk@13.1.0
#:package Aspire.Hosting.JavaScript@13.1.0
#:package Aspire.Hosting.Python@13.1.0

var builder = DistributedApplication.CreateBuilder(args);

// Python FastAPI (uvicorn, port 8000)
var app = builder.AddUvicornApp("app", "./app", "main:app")
    .WithUv()
    .WithArgs("--host", "0.0.0.0", "--port", "8000")
    .WithHttpsEndpoint(port: 8000, name: "api", isProxied: false)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health", endpointName: "api");

// .NET Workflow (port 5231)
var dotnetworkflow = builder.AddCSharpApp("dotnetworkflow", "./dotnetworkflow/FactoryWorkflow.csproj")
    .WithExternalHttpEndpoints()
    .WithReference(app)
    .WithEnvironment("MAINTENANCE_SCHEDULER_AGENT_URL", ...)
    .WithEnvironment("PARTS_ORDERING_AGENT_URL", ...);

// React Frontend (Vite dev server)
var frontend = builder.AddViteApp("frontend", "./frontend")
    .WithReference(dotnetworkflow)
    .WithReference(app)
    .WaitFor(app).WaitFor(dotnetworkflow);
```

Aspire provides:
- **Service discovery** - Services reference each other by name, Aspire resolves URLs
- **Health monitoring** - Health check endpoints (`/health`) on each service
- **Environment injection** - A2A URLs injected as environment variables
- **Development dashboard** - Visual monitoring of all services
- **Container publishing** - `PublishWithContainerFiles` bundles frontend statics into Python app

## Observability Stack

### OpenTelemetry (Python)

```python
from agent_framework.observability import configure_otel_providers
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor

configure_otel_providers()
FastAPIInstrumentor.instrument_app(app)
```

### OpenTelemetry (.NET)

```csharp
Sdk.CreateTracerProviderBuilder()
    .AddSource("FactoryWorkflow", "ChatClient", "Microsoft.Agents.AI*")
    .AddSource("AnomalyClassificationAgent", "FaultDiagnosisAgent", ...)
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddOtlpExporter()
    .AddAzureMonitorTraceExporter(options => options.ConnectionString = appInsightsConnStr);
```

### Telemetry destinations:
1. **Application Insights** - Azure-native monitoring
2. **OTLP exporter** - For local Aspire dashboard or external collectors

## Frontend Architecture

**Stack:** React 19 + TypeScript + Vite 7 (no UI library, custom CSS)

### Components

| Component | Purpose |
|-----------|---------|
| `App.tsx` | Root - manages workflow state, API calls, demo mode |
| `AlarmForm.tsx` | Input form - machine ID + telemetry JSON |
| `AgentIllustration.tsx` | Pipeline visualization - 5 agent nodes with status |

### Workflow Response Type

```typescript
interface WorkflowResponse {
  agentSteps: AgentStepResult[];
  finalMessage: string | null;
}

interface AgentStepResult {
  agentName: string;
  toolCalls: ToolCallInfo[];
  textOutput: string;
  finalMessage: string | null;
}
```

### Agent Name Normalization

The frontend maps raw agent names to display-friendly labels:
```typescript
function normalizeAgentName(name: string): AgentType {
  // Maps to: 'anomaly' | 'diagnosis' | 'planner' | 'scheduler' | 'parts'
}
```

### API Proxy (Development)

```typescript
// vite.config.ts
proxy: {
  '/api': {
    target: process.env.APP_HTTPS || 'http://localhost:8000',
    changeOrigin: true
  }
}
```

## Key Design Decisions and Workarounds

### 1. TextOnlyAgentExecutor

**Problem:** When passing conversation history between agents in a workflow, MCP tool call entries in the message history cause deserialization errors in the Microsoft.Agents SDK.

**Solution:** `TextOnlyAgentExecutor` wraps each agent, extracting only the final text output and passing that as a fresh message to the next agent. This loses tool call context but enables the pipeline to work.

### 2. WorkOrder Partition Key `/status`

**Problem:** Cosmos DB partitions by `/status`, but status updates change the partition key value.

**Solution:** To update status: read the document, delete from old partition, reinsert into new partition. The `CosmosDbService.update_work_order_status()` method handles this.

### 3. Challenge 3 Code Reuse in Challenge 4

**Problem:** Challenge 4 needs the same agent logic from Challenge 3 but wrapped as A2A services.

**Solution:** Challenge 4's `agents.py` adds Challenge 3's path to `sys.path` and imports the agent classes directly:
```python
CHALLENGE_3_PATH = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "challenge-3", "agents"))
sys.path.insert(0, CHALLENGE_3_PATH)
```

### 4. Mock Fallbacks

Both the maintenance windows and suppliers queries fall back to mock data if Cosmos DB queries fail. This ensures the pipeline doesn't break during development when seed data might be incomplete.

### 5. Persistent Memory Pruning

Chat histories are pruned to the last 10 messages to prevent unbounded growth. Only the last 5 are included in prompts to stay within context limits while preserving recent conversation flow.
