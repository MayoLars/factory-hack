# Agent Implementation Reference

Complete specifications for all 5 agents: their SDK patterns, system prompts, tools, input/output schemas, and code patterns.

## Agent Pipeline Overview

```
Telemetry Data
     ↓
[1] Anomaly Classification Agent (Python, function-tools or MCP-tools)
     ↓ output: {status, alerts[], summary}
[2] Fault Diagnosis Agent (Python, MCP + Foundry IQ)
     ↓ output: {MachineId, FaultType, RootCause, Severity, Metadata}
[3] Repair Planner Agent (C#/.NET, Foundry Agents SDK)
     ↓ output: WorkOrder (saved to Cosmos DB)
[4] Maintenance Scheduler Agent (Python, agent-framework + persistent memory)
     ↓ output: MaintenanceSchedule (saved to Cosmos DB)
[5] Parts Ordering Agent (Python, agent-framework + persistent memory)
     ↓ output: PartsOrder (saved to Cosmos DB)
```

---

## Agent 1: Anomaly Classification Agent

**Language:** Python
**Pattern:** `agent_framework.azure.AzureAIClient` with function tools
**File:** `challenge-1/agents/anomaly_classification_agent.py`

### SDK Pattern (Function Tools)

```python
from agent_framework.azure import AzureAIClient
from azure.identity.aio import DefaultAzureCredential

# Define tools as plain Python functions
def get_thresholds(machine_type: str) -> list:
    """Get warning/critical thresholds for a machine type."""
    container = cosmos_client.get_database_client(db_name).get_container_client("Thresholds")
    items = list(container.query_items(
        query="SELECT * FROM c WHERE c.machineType = @type",
        parameters=[{"name": "@type", "value": machine_type}],
        enable_cross_partition_query=True
    ))
    return items

def get_machine_data(machine_id: str) -> dict:
    """Get machine information by ID."""
    container = cosmos_client.get_database_client(db_name).get_container_client("Machines")
    items = list(container.query_items(
        query="SELECT * FROM c WHERE c.id = @id",
        parameters=[{"name": "@id", "value": machine_id}],
        enable_cross_partition_query=True
    ))
    return items[0] if items else {}

# Create and run agent
async with DefaultAzureCredential() as credential:
    async with AzureAIClient(credential=credential).create_agent(
        name="AnomalyClassificationAgent",
        instructions=AGENT_INSTRUCTIONS,
        tools=[get_machine_data, get_thresholds]
    ) as agent:
        result = await agent.run(prompt)
        print(result.text)
```

### Alternative SDK Pattern (MCP Tools via Foundry)

```python
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import MCPTool, PromptAgentDefinition

project_client = AIProjectClient(endpoint=project_endpoint, credential=DefaultAzureCredential())
agent = project_client.agents.create_version(
    agent_name="AnomalyClassificationAgent",
    definition=PromptAgentDefinition(
        model="gpt-4.1",
        instructions=AGENT_INSTRUCTIONS,
        tools=[
            MCPTool(server_label="machine-data",
                    server_url=machine_data_mcp_endpoint,
                    require_approval="never",
                    project_connection_id="machine-data-connection"),
            MCPTool(server_label="maintenance-data",
                    server_url=maintenance_data_mcp_endpoint,
                    require_approval="never",
                    project_connection_id="maintenance-data-connection")
        ]
    )
)
```

### System Prompt

```
You are an Anomaly Classification Agent for tire manufacturing equipment.
Analyze sensor telemetry data and classify anomalies by comparing readings
against operational thresholds.

Use the provided tools to:
1. Get machine data to identify the machine type
2. Get thresholds for that machine type
3. Compare telemetry readings against warning and critical thresholds

Output format (JSON):
{
  "status": "high" | "medium",
  "alerts": [
    {
      "name": "<metric name>",
      "severity": "warning" | "critical",
      "description": "<explanation>"
    }
  ],
  "summary": {
    "totalRecordsProcessed": <int>,
    "violations": {
      "critical": <int>,
      "warning": <int>
    }
  }
}
```

---

## Agent 2: Fault Diagnosis Agent

**Language:** Python
**Pattern:** `AIProjectClient` with MCP tools + Foundry IQ knowledge base
**File:** `challenge-1/agents/fault_diagnosis_agent.py`

### SDK Pattern

```python
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import MCPTool, PromptAgentDefinition
from azure.identity import DefaultAzureCredential

project_client = AIProjectClient(endpoint=project_endpoint, credential=DefaultAzureCredential())

agent = project_client.agents.create_version(
    agent_name="FaultDiagnosisAgent",
    description="Fault diagnosis agent",
    definition=PromptAgentDefinition(
        model="gpt-4.1",
        instructions=INSTRUCTIONS,
        tools=[
            MCPTool(
                server_label="machine-data",
                server_url=machine_data_mcp_endpoint,  # APIM -> Cosmos
                require_approval="never",
                project_connection_id="machine-data-connection"
            ),
            MCPTool(
                server_label="machine-wiki",
                server_url=machine_wiki_mcp_endpoint,  # Foundry IQ -> AI Search
                require_approval="never",
                project_connection_id="machine-wiki-connection"
            )
        ]
    )
)

# Test via OpenAI conversations API
openai_client = project_client.get_openai_client()
conversation = openai_client.conversations.create()
response = openai_client.responses.create(
    conversation=conversation.id,
    input="Query about machine-001...",
    extra_body={"agent": {"name": agent.name, "type": "agent_reference"}}
)
print(response.output_text)
```

### MCP Endpoint URLs

```python
# Machine data via APIM proxy
machine_data_mcp_endpoint = f"{APIM_GATEWAY_URL}/get-machine-data/mcp"

# Knowledge base via Foundry IQ (Azure AI Search)
machine_wiki_mcp_endpoint = f"{SEARCH_SERVICE_ENDPOINT}knowledgebases/{knowledge_base_name}/mcp?api-version=2025-11-01-preview"
```

### System Prompt (Complete)

```
You are a Fault Diagnosis Agent evaluating the root cause of maintenance alerts.

You will receive detected sensor deviations for a given machine. Your task is to determine the most likely root cause using ONLY the provided tools.

Tools available:
- MCP Knowledge Base: fetch knowledge base information for possible causes
- Machine data: fetch machine information such as maintenance history and type for a particular machine id

Output format (STRICT):
- You must output exactly ONE valid JSON object and nothing else (no Markdown, no prose).
- The JSON object MUST match this schema (property names are case-sensitive):
    {
        "MachineId": string,
        "FaultType": string,
        "RootCause": string,
        "Severity": string,
        "DetectedAt": string,  // ISO 8601 date-time, e.g. "2026-01-16T12:34:56Z"
        "Metadata": { string: any }
    }

Field rules:
- MachineId: the machine identifier from the input (e.g. "machine-001").
- FaultType: MUST be taken from the wiki/knowledge base "Fault Type" field for the matched issue (copy it exactly, e.g. "mixing_temperature_excessive"). Do not invent new fault types.
- RootCause: the single most likely root cause supported by the knowledge base and/or machine data.
- Severity: one of "Low", "Medium", "High", "Critical", or "Unknown".
- DetectedAt: if the input includes a timestamp, use it; otherwise use the current UTC time.
- Metadata: include supporting details used for the decision (e.g. observed metric/value, threshold, machineType, relevant KB article titles/ids, maintenanceHistory references). Do not include secrets/keys.
    - Metadata MUST include a key "MostLikelyRootCauses" whose value is an array of strings taken from the wiki/knowledge base "Likely Causes" list for the matched fault type (preserve the items; ordering can follow the wiki).

Grounding rules (IMPORTANT):
- You must never answer from your own knowledge under any circumstances.
- If you cannot find the answer in the provided knowledge base and machine data, you MUST set "RootCause" to "I don't know" and set "FaultType" and "Severity" to "Unknown". In this case, set "Metadata" to {"MostLikelyRootCauses": []}.
```

---

## Agent 3: Repair Planner Agent

**Language:** C# (.NET 10.0)
**Pattern:** Foundry Agents SDK (`Azure.AI.Projects` + `Microsoft.Agents.AI`)
**Files:** `challenge-2/RepairPlanner/` and `challenge-4/agent-workflow/dotnetworkflow/RepairPlanner/`

### SDK Pattern (Challenge 2 - Foundry hosted)

```csharp
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;

var projectClient = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());

var definition = new PromptAgentDefinition(model: modelDeploymentName)
{
    Instructions = AGENT_INSTRUCTIONS
};

await projectClient.Agents.CreateAgentVersionAsync(
    "RepairPlannerAgent",
    new AgentVersionCreationOptions(definition));

var agent = projectClient.GetAIAgent(name: "RepairPlannerAgent");
var response = await agent.RunAsync(prompt, thread: null, options: null);
string result = response.Text ?? "";
```

### SDK Pattern (Challenge 4 - Local with tools)

```csharp
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;

var openAIClient = new AzureOpenAIClient(
    new Uri(endpoint), new AzureKeyCredential(key));
var chatClient = openAIClient.GetChatClient(modelDeploymentName)
    .AsIChatClient();

var tools = new[]
{
    AIFunctionFactory.Create(GetAvailableTechnicians),
    AIFunctionFactory.Create(GetAvailableParts),
    AIFunctionFactory.Create(CreateWorkOrder)
};

var agent = chatClient.CreateAIAgent(
    name: "RepairPlannerAgent",
    instructions: AGENT_INSTRUCTIONS,
    tools: tools);
```

### System Prompt

```
You are a Repair Planner Agent for tire manufacturing equipment.
Generate a repair plan with tasks, timeline, and resource allocation.
Return the response as valid JSON matching the WorkOrder schema.

Output JSON with these fields:
- workOrderNumber, machineId, title, description
- type: "corrective" | "preventive" | "emergency"
- priority: "critical" | "high" | "medium" | "low"
- status, assignedTo (technician id or null), notes
- estimatedDuration: integer (minutes, e.g. 60 not "60 minutes")
- partsUsed: [{ partId, partNumber, quantity }]
- tasks: [{ sequence, title, description, estimatedDurationMinutes (integer), requiredSkills, safetyNotes }]

IMPORTANT: All duration fields must be integers representing minutes (e.g. 90), not strings.

Rules:
- Assign the most qualified available technician
- Include only relevant parts; empty array if none needed
- Tasks must be ordered and actionable
```

### NuGet Dependencies (Exact Versions)

```xml
<PackageReference Include="Azure.AI.Projects" Version="1.2.0-beta.5" />
<PackageReference Include="Azure.Identity" Version="1.17.1" />
<PackageReference Include="Microsoft.Agents.AI" Version="1.0.0-preview.260108.1" />
<PackageReference Include="Microsoft.Agents.AI.AzureAI" Version="1.0.0-preview.260108.1" />
<PackageReference Include="Microsoft.Extensions.AI" Version="10.2.0" />
<PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="10.2.0" />
<PackageReference Include="Microsoft.Azure.Cosmos" Version="3.56.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
```

### Critical C# Implementation Notes

1. **Dual JSON attributes** - Use both `System.Text.Json.Serialization.JsonPropertyName` and `Newtonsoft.Json.JsonProperty` on model classes (Cosmos SDK uses Newtonsoft internally)

2. **Number handling** - LLMs return numbers as strings. Always use:
   ```csharp
   new JsonSerializerOptions {
       PropertyNameCaseInsensitive = true,
       NumberHandling = JsonNumberHandling.AllowReadingFromString
   };
   ```

3. **Suppress warnings** - Add `<NoWarn>$(NoWarn);CA2252</NoWarn>` for preview API warnings

4. **FaultMappingService** - Hardcoded in-memory dictionaries (see `02-DATA-MODEL.md` for complete mappings)

---

## Agent 4: Maintenance Scheduler Agent

**Language:** Python
**Pattern:** `agent_framework.azure.AzureAIClient` with persistent memory (Cosmos DB chat history)
**File:** `challenge-3/agents/maintenance_scheduler_agent.py`

### SDK Pattern

```python
from agent_framework.azure import AzureAIClient
from azure.identity.aio import AzureCliCredential

async with AzureCliCredential() as credential:
    async with AzureAIClient(credential=credential).create_agent(
        name="MaintenanceSchedulerAgent",
        description="Predictive maintenance scheduling agent",
        instructions=INSTRUCTIONS,
    ) as agent:
        result = await agent.run(full_prompt)
        response_text = result.text
```

### Persistent Memory Pattern

```python
# Load history (last 5 messages prepended to prompt)
chat_history_json = await cosmos_service.get_machine_chat_history(machine_id)
if chat_history_json:
    history_msgs = json.loads(chat_history_json)
    history_context = "\n".join(
        [f"{msg['role']}: {msg['content']}" for msg in history_msgs[-5:]]
    )
    full_prompt = f"Previous conversation context:\n{history_context}\n\n{context}"

# After agent response, save interaction
messages = json.loads(existing_json) if existing_json else []
messages.append({"role": "user", "content": user_prompt})
messages.append({"role": "assistant", "content": assistant_response})
messages = messages[-10:]  # Keep last 10 messages
await cosmos_service.save_machine_chat_history(machine_id, json.dumps(messages))
```

### System Prompt

```
You are a predictive maintenance expert specializing in industrial tire manufacturing equipment.

Analyze historical maintenance data and recommend optimal maintenance schedules based on:
1. Historical failure patterns
2. Risk scores (time since last maintenance, fault frequency, downtime costs, criticality)
3. Optimal maintenance windows considering production impact
4. Detailed reasoning

Always respond in valid JSON format as requested.
```

### Context Building (Prompt Engineering)

The agent receives a rich context document built from:

1. **Work Order Information** - ID, machine ID, fault type, priority, estimated duration
2. **Historical Maintenance Data** - Total events, similar fault occurrences, MTBF analysis, failure cycle progress %
3. **Available Maintenance Windows** - Next 14 days of available slots with production impact ratings

### Output Schema

```json
{
  "scheduledDate": "<ISO datetime>",
  "maintenanceWindow": {
    "id": "<window ID>",
    "startTime": "<ISO datetime>",
    "endTime": "<ISO datetime>",
    "productionImpact": "Low|Medium|High",
    "isAvailable": true
  },
  "riskScore": 0-100,
  "predictedFailureProbability": 0.0-1.0,
  "recommendedAction": "IMMEDIATE|URGENT|SCHEDULED|MONITOR",
  "reasoning": "<detailed explanation>"
}
```

### Foundry Portal Registration

The agent also registers itself in the Azure AI Foundry portal:

```python
from azure.ai.projects.models import PromptAgentDefinition

registered_agent = await project_client.agents.create_version(
    agent_name="MaintenanceSchedulerAgent",
    definition=definition,
    description="Predictive maintenance scheduling agent",
    metadata={
        "framework": "agent-framework",
        "purpose": "maintenance_scheduling",
        "timestamp": datetime.utcnow().isoformat(),
    },
)
```

---

## Agent 5: Parts Ordering Agent

**Language:** Python
**Pattern:** Same as Maintenance Scheduler (agent-framework + persistent memory)
**File:** `challenge-3/agents/parts_ordering_agent.py`

### Flow

1. Load work order from Cosmos DB
2. Check `PartsInventory` for required parts
3. Find suppliers from `Suppliers` container (matched by part number)
4. Load persistent chat history for this work order
5. Call AI agent with context (inventory levels, reorder points, supplier lead times, reliability ratings)
6. Parse JSON order response
7. Save `PartsOrder` to Cosmos DB
8. Update work order status to "PartsOrdered" or "Ready"

### System Prompt (Approximate)

```
You are a Parts Ordering Agent for tire manufacturing equipment maintenance.

Analyze inventory status and generate optimized parts orders considering:
1. Current stock levels vs. reorder points
2. Supplier lead times and reliability ratings
3. Order consolidation opportunities
4. Cost optimization

Output JSON with: supplierId, supplierName, orderItems[], totalCost, expectedDeliveryDate
```

---

## A2A Protocol (Challenge 4 Integration)

### Python A2A Server Pattern

```python
from a2a.server.apps import A2AStarletteApplication
from a2a.server.request_handlers import DefaultRequestHandler
from a2a.server.agent_execution import AgentExecutor, RequestContext
from a2a.server.events.event_queue import EventQueue
from a2a.server.tasks import InMemoryTaskStore
from a2a.types import AgentCard, AgentCapabilities, AgentSkill, TextPart, Message

agent_card = AgentCard(
    name="MaintenanceSchedulerAgent",
    description="...",
    url="https://localhost:8000/maintenance-scheduler/",
    version="1.0.0",
    capabilities=AgentCapabilities(streaming=True, pushNotifications=False),
    defaultInputModes=["text"],
    defaultOutputModes=["text"],
    skills=[AgentSkill(id="schedule_maintenance", name="Schedule Maintenance", description="...")]
)

class MyExecutor(AgentExecutor):
    async def execute(self, context: RequestContext, event_queue: EventQueue) -> None:
        # Extract input text from context.message.parts
        # Run agent logic
        # Send response
        response_message = Message(
            messageId=str(uuid.uuid4()),
            role="agent",
            parts=[TextPart(text=response_text)]
        )
        await event_queue.enqueue_event(response_message)

    async def cancel(self, context, event_queue):
        pass

app = A2AStarletteApplication(
    agent_card=agent_card,
    http_handler=DefaultRequestHandler(
        agent_executor=MyExecutor(),
        task_store=InMemoryTaskStore()
    )
)

# Mount into FastAPI
fastapi_app.mount("/maintenance-scheduler", app.build())
```

### C# A2A Client Pattern

```csharp
// Resolve agent from A2A endpoint
var cardResolver = new A2ACardResolver(new Uri(url.TrimEnd('/') + "/"));
var agent = await cardResolver.GetAIAgentAsync();
// agent is now an AIAgent usable in workflow pipeline
```

### C# Workflow Builder Pattern

```csharp
// TextOnlyAgentExecutor strips MCP tool history between agents
var executors = agents.Select(a => new TextOnlyAgentExecutor(a)).ToList();

TextOnlyAgentExecutor.ClearResults();

var workflowBuilder = new WorkflowBuilder(executors[0]);
for (int i = 1; i < executors.Count; i++)
{
    workflowBuilder.BindExecutor(executors[i]);
    workflowBuilder.AddEdge(executors[i - 1], executors[i]);
}
workflowBuilder.WithOutputFrom(executors[^1]);

var workflow = workflowBuilder.Build();
var run = await InProcessExecution.Default.RunAsync<string>(workflow, input);
```

---

## SDK Version Matrix

### Python Packages (pre-release, use `pip install --pre`)

```
agent-framework-azure-ai==1.0.0b260107
agent-framework-azure-ai-search==1.0.0b260107
agent-framework-core==1.0.0b260107
agent-framework-a2a>=1.0.0b260107
azure-ai-projects==2.0.0b3
azure-cosmos>=4.5.0
azure-identity>=1.15.0
azure-search-documents>=11.7.0b2
azure-ai-inference[tracing]>=1.0.0b6
azure-monitor-opentelemetry>=1.2.0
opentelemetry-semantic-conventions-ai==0.4.13
```

### .NET Packages (Challenge 2)

```xml
Azure.AI.Projects                          1.2.0-beta.5
Microsoft.Agents.AI                        1.0.0-preview.260108.1
Microsoft.Agents.AI.AzureAI                1.0.0-preview.260108.1
Microsoft.Extensions.AI                    10.2.0
Microsoft.Azure.Cosmos                     3.56.0
Azure.Identity                             1.17.1
```

### .NET Packages (Challenge 4 - additional)

```xml
Microsoft.Agents.AI.A2A                    1.0.0-preview.260108.1
Microsoft.Agents.AI.AzureAI.Persistent    1.0.0-preview.260108.1
Microsoft.Agents.AI.Workflows             1.0.0-preview.260108.1
Azure.Monitor.OpenTelemetry.Exporter       1.4.0
OpenTelemetry.Exporter.OpenTelemetryProtocol 1.13.1
Serilog.AspNetCore                         9.0.0
DotNetEnv                                  3.1.1
```
