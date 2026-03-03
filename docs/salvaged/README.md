# Salvaged Runtime Artifacts

Captured from a live Aspire orchestration session on 2026-03-03, after Azure environment access was revoked. These files preserve the runtime behavior and execution traces that cannot be recreated from source code alone.

## Key Files

### Execution Logs (from Aspire temp dir `/tmp/aspire-dcpKlqKLO/`)

| File | Size | Contents |
|------|------|----------|
| `dotnet-workflow-stdout.txt` | 205K | Full .NET workflow execution trace — contains a complete successful 5-agent pipeline run for machine-001. Shows agent orchestration, tool calls, text outputs, and timing. Work order WO-20260303-131822 created. |
| `python-fastapi-stderr.txt` | 132K | Python agent execution with full A2A message content. Contains complete repair plan text passed between agents, Cosmos DB SDK request/response traces, work order wo-2026-cdf6318a details. |
| `python-fastapi-stdout.txt` | 15K | HTTP request log showing A2A agent card resolution and POST calls between services. |
| `aspire-dashboard.txt` | 5.1K | Aspire dashboard startup — ports, OTLP gRPC endpoint, login URL. |
| `frontend-vite.txt` | 396B | Vite dev server startup log. |
| `frontend-installer.txt` | 548B | npm install output for frontend dependencies. |
| `python-installer-stderr.txt` | 111B | uv pip install output for Python dependencies. |

### A2A Protocol Artifacts (captured live from running services)

| File | Contents |
|------|----------|
| `a2a-maintenance-scheduler-card.json` | Live A2A agent card from `https://localhost:8000/maintenance-scheduler/.well-known/agent-card.json` — protocol v0.3.0, JSONRPC transport, streaming capability. |
| `a2a-parts-ordering-card.json` | Live A2A agent card from `https://localhost:8000/parts-ordering/.well-known/agent-card.json`. |

### API Specs

| File | Contents |
|------|----------|
| `fastapi-openapi-spec.json` | OpenAPI 3.1.0 spec captured from the running FastAPI service — documents `/api/analyze_machine`, `/api/weatherforecast`, `/health`, and A2A sub-app mount points. |

### Notebooks

| File | Contents |
|------|----------|
| `create_knowledge_base.ipynb` | Jupyter notebook with cell outputs from creating the Foundry IQ knowledge base — shows Azure AI Search index creation, blob data source setup, and embedding model configuration. |

### Aspire Resource Logs

The `resource-executable-*.txt` and `resource-service-*.txt` files are Aspire DCP (Distributed Cloud Platform) internal logs for process management and service health monitoring. They contain process lifecycle events but no application-level data.

## What's Valuable Here

1. **The .NET workflow log** (`dotnet-workflow-stdout.txt`) is the most valuable — it contains a complete end-to-end pipeline execution showing exactly what each agent receives as input and produces as output.

2. **The Python stderr log** (`python-fastapi-stderr.txt`) contains the full A2A message payloads including the complete repair plan text that the maintenance scheduler and parts ordering agents received.

3. **The A2A agent cards** document the exact protocol version and capabilities used.

4. **The OpenAPI spec** documents the actual API surface of the running system.
