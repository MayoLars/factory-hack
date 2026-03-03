# Knowledge Base Wiki Content

This document preserves the content of the machine wiki markdown files that are uploaded to Azure Blob Storage and indexed into the Foundry IQ knowledge base. These files are the grounding source for the Fault Diagnosis Agent.

The original files are located in `challenge-0/data/kb-wiki/`.

## Setup Process

1. Wiki markdown files are uploaded to Azure Blob Storage container `machine-wiki`
2. A Jupyter notebook (`challenge-1/create_knowledge_base.ipynb`) creates an Azure AI Search index named `machine-kb`
3. The content is indexed using the embedding model (`text-embedding-3-large`)
4. The Fault Diagnosis Agent accesses this via Foundry IQ MCP endpoint:
   ```
   https://<search-service>.search.windows.net/knowledgebases/machine-kb/mcp?api-version=2025-11-01-preview
   ```

## Wiki File Inventory

| File | Machine Type | Content |
|------|-------------|---------|
| `banbury_mixer.md` | banbury_mixer | Troubleshooting for Banbury Mixer E1 |
| `tire_building_machine.md` | tire_building_machine | Troubleshooting for Tire Building Machine B1 |
| `tire_curing_press.md` | tire_curing_press | Troubleshooting for Tire Curing Press A1 |
| `tire_extruder.md` | tire_extruder | Troubleshooting for Tire Extruder C1 |
| `tire_uniformity_machine.md` | tire_uniformity_machine | Troubleshooting for Tire Uniformity Machine D1 |

## Critical Content Structure

Each wiki file follows a consistent structure that the Fault Diagnosis Agent relies on:

1. **Machine overview** - Name, model, specifications
2. **Common fault types** - Each with:
   - **Fault Type** identifier (e.g., `curing_temperature_excessive`) - the agent copies this exactly
   - **Likely Causes** list - the agent includes these in `Metadata.MostLikelyRootCauses`
   - **Diagnostic steps**
   - **Repair procedures**
   - **Severity classification**

The Fault Diagnosis Agent's system prompt requires it to:
- Copy the `Fault Type` field exactly as written in the wiki (snake_case identifiers)
- Include the `Likely Causes` list verbatim in the `MostLikelyRootCauses` metadata field
- Never invent new fault types outside what the wiki defines
- Return "I don't know" if the wiki doesn't cover the observed symptoms

## Recreating the Knowledge Base

To recreate in a new environment:

1. Upload the 5 markdown files from `challenge-0/data/kb-wiki/` to a blob container
2. Open `challenge-1/create_knowledge_base.ipynb` and update:
   - `AZURE_AI_PROJECT_ENDPOINT`
   - `AZURE_SEARCH_ENDPOINT`
   - `AZURE_SEARCH_API_KEY`
   - `EMBEDDING_MODEL_DEPLOYMENT_NAME` (use `text-embedding-3-large`)
   - Storage connection for blob data source
3. Run the notebook to create the search index and Foundry IQ knowledge base
4. Verify the MCP endpoint is accessible:
   ```
   curl -H "api-key: <SEARCH_API_KEY>" \
     "https://<search>.search.windows.net/knowledgebases/machine-kb/mcp?api-version=2025-11-01-preview"
   ```
5. Create the `machine-wiki-connection` project connection in AI Foundry portal pointing to this endpoint
