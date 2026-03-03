# Development Environment and Tooling

Complete specification of the development container, toolchain, and IDE configuration.

## Dev Container

**File:** `.devcontainer/devcontainer.json`

### Base Image
```
mcr.microsoft.com/devcontainers/python:1-3.11-bookworm
```

### Installed Features

| Feature | Version | Purpose |
|---------|---------|---------|
| Azure CLI + Bicep | latest | Azure resource management |
| uv (Astral) | latest | Fast Python package manager |
| .NET Aspire | latest | Multi-service orchestration |
| Node.js | lts | Frontend development |
| .NET SDK | 10.0 | C# agent development |

### Post-Create Script

`post-create.sh` runs after container creation:
```bash
pip install --pre -r requirements.txt
dotnet restore factory-hack.sln
```

### VS Code Extensions

| Extension | Purpose |
|-----------|---------|
| GitHub Copilot + Chat | AI-assisted development |
| Python + Pylance | Python language support |
| C# Dev Kit | .NET development |
| Jupyter | Notebook support (knowledge base setup) |
| Azure Tools | Azure resource management |
| AI Studio Extension | Azure AI Foundry integration |

### Port Forwarding
- **5231**: .NET Workflow API

## Solution Structure

### .NET Solution (`factory-hack.sln`)

Only includes Challenge 4's workflow project:
```
factory-hack.sln
  └── challenge-4/agent-workflow/dotnetworkflow/FactoryWorkflow.csproj
```

Challenge 2's `RepairPlanner.csproj` is standalone (not in the solution).

### Python Requirements

**Root `requirements.txt`** (install with `--pre` flag):
```
agent-framework-azure-ai==1.0.0b260107
agent-framework-azure-ai-search==1.0.0b260107
agent-framework-core==1.0.0b260107
azure-ai-projects==2.0.0b3
azure-cosmos>=4.5.0
azure-identity>=1.15.0
azure-search-documents>=11.7.0b2
azure-ai-inference[tracing]>=1.0.0b6
azure-monitor-opentelemetry>=1.2.0
opentelemetry-api>=1.20.0
opentelemetry-sdk>=1.20.0
opentelemetry-exporter-otlp-proto-grpc>=1.20.0
opentelemetry-semantic-conventions-ai==0.4.13
dataclasses-json>=0.6.0
python-dotenv>=1.0.0
ipykernel
pytest-asyncio>=1.3.0
```

**Challenge 4 Python app `pyproject.toml`** (managed by uv):
```toml
[project]
requires-python = ">=3.13"
dependencies = [
    "fastapi[standard]>=0.119.0",
    "agent-framework>=0.1.0",
    "agent-framework-azure-ai>=0.1.0",
    "agent-framework-a2a>=1.0.0b260107",
    "azure-identity>=1.15.0",
    "azure-cosmos>=4.5.0",
    "python-dotenv>=1.0.0",
    "opentelemetry-distro>=0.59b0",
    "opentelemetry-exporter-otlp-proto-grpc>=1.38.0",
    "opentelemetry-instrumentation-fastapi>=0.59b0",
]
[tool.uv]
prerelease = "allow"
```

### Frontend Dependencies

```json
{
  "dependencies": {
    "react": "^19.2.1",
    "react-dom": "^19.2.1"
  },
  "devDependencies": {
    "@vitejs/plugin-react": "^5.1.1",
    "typescript": "~5.9.3",
    "vite": "^7.2.6"
  }
}
```

## GitHub Copilot Agent Configurations

### `.github/copilot-instructions.md`
Workshop-mode instructions directing Copilot to help build from context, not from solution code.

### `.github/agents/agentplanning.agent.md`
Custom Copilot agent for Challenge 2 (.NET Repair Planner):
- Enforces Foundry Agents SDK pattern
- Contains canonical fault-to-skills/parts mappings
- Specifies exact NuGet package versions
- Guides small-model-friendly code generation

### `.github/agents/hackagent.agent.md`
General hackathon builder agent for challenge folder structure and Azure setup.

### `.github/agents/knowledge/`
- `factory-hack.md` - Domain knowledge about the factory scenario
- `claimsv2-hack.md` - Alternative hackathon scenario (not used)

## Key File Paths Reference

```
/                              # Repo root
├── .env                       # Generated secrets (gitignored)
├── .env.example               # Template
├── requirements.txt           # Python dependencies
├── factory-hack.sln           # .NET solution
├── CLAUDE.md                  # Claude Code instructions
│
├── challenge-0/
│   ├── get-keys.sh            # Extract Azure keys → .env
│   ├── seed-data.sh           # Seed Cosmos + Blob + APIM
│   ├── infra/azuredeploy.json # ARM template
│   └── data/                  # All seed data JSON + wiki markdown
│
├── challenge-1/
│   ├── create_knowledge_base.ipynb  # Foundry IQ setup
│   └── agents/
│       ├── anomaly_classification_agent.py      # Function tools
│       ├── anomaly_classification_agent_mcp.py  # MCP tools
│       └── fault_diagnosis_agent.py             # MCP + Foundry IQ
│
├── challenge-2/RepairPlanner/   # Standalone C# project
│
├── challenge-3/
│   ├── run-batch.py             # Batch runner
│   └── agents/
│       ├── maintenance_scheduler_agent.py
│       ├── parts_ordering_agent.py
│       └── services/
│           ├── cosmos_db_service.py    # Shared data layer
│           └── observability.py
│
└── challenge-4/agent-workflow/
    ├── apphost.cs               # Aspire orchestrator (C# script)
    ├── app/                     # Python FastAPI + A2A
    │   ├── main.py
    │   ├── agents.py            # Workflow + A2A wrappers
    │   └── telemetry.py
    ├── dotnetworkflow/          # .NET Workflow API
    │   ├── Program.cs
    │   ├── AgentProviders.cs    # Agent/A2A/Local providers
    │   ├── Models.cs
    │   ├── TextOnlyAgentExecutor.cs
    │   └── RepairPlanner/       # Embedded RepairPlanner (Ch4 variant)
    └── frontend/                # React + Vite
        └── src/
            ├── App.tsx
            ├── components/
            └── types/
```
