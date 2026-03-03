# Challenge 1 Notes

## Embedding Model Fix

The hackathon template expects `text-embedding-ada-002` but the deployed Azure OpenAI resource has `text-embedding-3-large` instead.

When creating the knowledge source in Task 3.2, use `text-embedding-3-large` for the embedding model deployment name. If you already created the knowledge source with the wrong model, you must:

1. Delete the knowledge base (`machine-kb`)
2. Delete the knowledge source (`machine-wiki-blob-ks`)
3. Delete the leftover Search indexer (`machine-wiki-blob-ks-indexer`)
4. Recreate knowledge source and knowledge base with `text-embedding-3-large`

The `.env` variable `EMBEDDING_MODEL_DEPLOYMENT_NAME` was also updated from `text-embedding-ada-002` to `text-embedding-3-large`.
