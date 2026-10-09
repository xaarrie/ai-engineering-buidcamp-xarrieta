# Migrate Agent Lessons to .NET

## Objective

Provide standalone .NET 10 equivalents for the two Python notebooks in `03-Agents/`, using Microsoft Agent Framework and the repository's existing lesson conventions.

## Problem and Why

The agent lessons currently exist only as Python notebooks. Learners following the .NET curriculum need equivalent, runnable examples that demonstrate tool calling and agentic RAG without duplicating already-tested ingestion, search, chunking, or context-formatting utilities.

## Authorized Scope

- Add `dotnet/03-Agents/AgenticLoop_dotnet.cs`.
- Add `dotnet/03-Agents/AgenticRAG_v1_dotnet.cs`.
- Reuse `dotnet/utilities/DocumentIngestion`, `MinSearch`, and `RagSupport` where applicable.
- Add a narrowly scoped shared utility only when both lessons demonstrably need it.
- Do not modify the Python notebooks or unrelated existing files.

## Constraints

- Use Microsoft Agent Framework with the repository's `Microsoft.Agents.AI.OpenAI@1.22.0` convention.
- Do not execute live model, OpenAI, GitHub, or document-download calls during verification.
- Keep lessons standalone, English, and file-based .NET 10 apps.
- Preserve all unrelated local changes and untracked files.

## Delivery Strategy

- Strategy: `ask-on-risk`.
- Forecast: approximately 600 authored lines across two independent lesson work units; if the running total exceeds 400 lines, ask once for a chain strategy before the next commit.

## Tasks

- [x] **ODD-006a — Agentic loop lesson**
  - Route: delegated direct; preparation and implementation require current Agent Framework tool API verification.
  - Acceptance: a standalone lesson defines a deterministic local tool, passes it to an Agent Framework agent, shows session/tool-calling flow, and includes an offline fixture that proves local tool behavior without a model call.
  - Checks: compile the file app; run its offline fixture; do not call external services.
  - Evidence: implemented in `dotnet/03-Agents/AgenticLoop_dotnet.cs`; offline and compile checks were attempted without restore, but both stopped before compilation because the file-app restore assets were not cached (`NETSDK1004`, missing `obj/project.assets.json`). The task remains unchecked until those local assets are available.

- [ ] **ODD-006b — Agentic RAG lesson**
  - Route: delegated direct; combines a lesson migration with existing ingestion, lexical-index, and context-formatting utilities.
  - Acceptance: a standalone lesson contrasts manual RAG with a tool-enabled agent, uses typed output, handles no-result retrieval, and has deterministic offline verification for the local RAG/tool path.
  - Checks: compile the file app; run offline fixtures; do not call external services.
  - Evidence: after user-authorized NuGet restore, `dotnet build dotnet/03-Agents/AgenticLoop_dotnet.cs` succeeded and `dotnet run --file dotnet/03-Agents/AgenticLoop_dotnet.cs -- --verify` passed. Independent static verification confirmed explicit model binding through `OpenAIClient.GetChatClient(model).AsIChatClient()`, framework-owned tool registration, and the offline branch's no-model/no-environment property. Live model execution was intentionally not run.

## Progress

- Exploration complete: two Python notebooks map to `AgenticLoop_dotnet.cs` and `AgenticRAG_v1_dotnet.cs`.
- Current Agent Framework evidence confirms `ChatClientAgent` accepts `IList<AITool>` and function tools can be built with `AIFunctionFactory`.
- ODD-006a complete; next step: commit its standalone lesson and then implement ODD-006b.
