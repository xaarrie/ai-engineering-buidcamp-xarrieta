# Foundation .NET Examples

## Goal
Complete missing .NET 10 file-based lesson examples for `01-Foundation`, preserving the learning intent of their corresponding notebooks and using Microsoft Agent Framework with the repository utilities.

## Scope
- Source curriculum: `01-Foundation/**`
- Target lessons: `dotnet/01-Foundation/**`
- Shared utilities: `dotnet/utilities/**` (reuse only; no changes unless an identified lesson requirement cannot be met)
- Preserve existing user changes outside this feature.

## Tasks
- [x] Map the Python/Jupyter curriculum to existing .NET lessons and confirm the missing lesson set. Evidence: the missing targets are `optional_v1_dotnet.cs`, `optional_v2_dotnet.cs`, and `rag_v5_dotnet.cs`; `RAG/homerwork1.ipynb` is an assignment, not a named example.
- [x] Implement the missing optional lesson examples as independent .NET 10 file-based apps using Microsoft Agent Framework. Uses OpenAI embeddings through the Agent Framework-compatible .NET client stack for independent vector/hybrid retrieval; `optional_v2` is self-contained local evaluation with descriptors, tests, report, regression comparison, and dashboard-style monitoring, while only Evidently Cloud upload/hosting is excluded.
- [x] Implement the remaining missing RAG lesson examples, reusing `DocumentIngestion`, `MinSearch`, `RagSupport`, and `StreamingJson` where applicable.
- [x] Build and execute each offline-capable lesson fixture; report online-only validation requirements. All three examples built and their offline fixtures passed; independent re-verification closed the two initial high-priority findings.

## Evidence
- Mapped all 11 notebooks in `01-Foundation/02-rag/` against the eight existing .NET lessons.
- Confirmed the existing Microsoft Agent Framework integration convention: `Microsoft.Agents.AI.OpenAI@1.22.0`, .NET 10 file-based apps, and root `.env` loading.
- Official Microsoft Agent Framework documentation confirms an embedding client surface; the selected approach will use the OpenAI embedding model named by `OPENAI_EMBEDDING_MODEL` (default `text-embedding-3-small`) with a small example-local vector ranking implementation because no shared vector-index utility exists.
- User decisions: OpenAI embeddings for `optional_v1`; local-only evaluation for `optional_v2`.
- Worker validation passed for all three file apps: `dotnet build <file>` and `dotnet run <file> -- --offline-fixture`; live model paths were intentionally not invoked.
- Independent verification initially found two high-priority gaps: vector retrieval was lexical pre-filtering and the local evaluation omitted report/test/monitoring concepts. Remediation made vector ranking independent over the complete corpus and added local descriptors, tests, structured reports, regression comparison, and dashboard-style monitoring. Re-verification closed both findings.
- Online OpenAI generation, embedding calls, and Evidently Cloud upload/hosting were intentionally not executed; their credentials/service behavior are outside offline validation.

## Commits
- No commit created: user did not request one.
