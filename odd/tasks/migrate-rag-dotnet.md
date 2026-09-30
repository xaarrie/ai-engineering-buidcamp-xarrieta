# RAG .NET Migration Work Units

## Objective

Migrate the two lessons in `01-Foundation/02-rag` into independent .NET 10 file-based applications that use the current verified Microsoft Agent Framework API while preserving their teaching goals.

## Scope

- **ODD-001 — OpenAI lesson:** migrate `openai_v1.ipynb` to `dotnet/01-Foundation/02-rag/openai_v1_dotnet.cs`.
- **ODD-002 — RAG ingestion lesson:** migrate `rag_v1.ipynb` to `dotnet/01-Foundation/02-rag/rag_v1_dotnet.cs`.
- **ODD-003a — Provider-neutral search library:** create an intentionally dependency-free, backend-neutral .NET 10 MinSearch-compatible search library and executable assertions for the later RAG v2 migration.
- **ODD-003b — RAG v2 local retrieval lesson:** integrate the local provider-neutral MinSearch abstraction into a .NET 10 file-based lesson that reads Markdown and MDX documents from a local directory.
- **ODD-003c — RAG v2 GitHub ZIP ingestion:** extend the RAG v2 lesson with the complete GitHub ZIP ingestion pattern from ODD-002 while retaining deterministic local input.
- **ODD-004a — Reusable DocumentIngestion library:** create a dependency-free .NET 10 library and executable assertions that own provider-neutral document ingestion; later RAG v1 and RAG v2 migrations consume it.
- **ODD-004b — RAG v1 DocumentIngestion integration:** refactor the RAG v1 lesson to consume the reusable source, path, frontmatter, and mapping contracts while retaining lesson-specific GitHub source configuration and entry visibility.
- **ODD-004c — RAG v2 DocumentIngestion integration:** refactor the RAG v2 lesson to consume reusable local/GitHub sources, path handling, frontmatter parsing, and mapping while retaining its MinSearch retrieval behavior.
- **ODD-005a — StreamingJson utility:** create a dependency-free .NET 10 SAX-style incremental JSON parser and executable assertions for the structured JSON streaming lesson.
- **ODD-005b — RAG support utility:** create a dependency-free .NET 10 deterministic chunking and context-formatting library with executable assertions for the populated structured RAG lesson.

## Constraints

- Follow `dotnet/instructions.md`: each lesson is a standalone .NET 10 file app with no project file.
- Load environment variables from the repository-root `.env` without logging secrets.
- Use the latest verified `Microsoft.Agent.Framework` integration for all model operations.
- Keep source code, comments, and documentation in English.
- Do not modify `rag_v1_dotnet.cs` during ODD-001.
- Do not stage or commit unrelated existing changes.

## Task Routes and Trigger Evidence

| ID | Route | Trigger evidence | Deliverable |
| --- | --- | --- | --- |
| ODD-001 | `01-Foundation/02-rag/openai_v1.ipynb` → `dotnet/01-Foundation/02-rag/openai_v1_dotnet.cs` | Notebook cells demonstrate basic and message requests, response details, token usage and cost, streaming, instructions, and stateless versus stateful conversation history. | Standalone OpenAI lesson file app. |
| ODD-002 | `01-Foundation/02-rag/rag_v1.ipynb` → `dotnet/01-Foundation/02-rag/rag_v1_dotnet.cs` | Source lesson downloads a GitHub ZIP in memory, lists entries, parses Markdown frontmatter, and prepares every document for later indexing. | Standalone RAG ingestion lesson file app. |
| ODD-003a | Authorized RAG v2 MinSearch-equivalent migration → `dotnet/utilities/MinSearch/MinSearch.csproj` and `dotnet/utilities/MinSearch.Tests/MinSearch.Tests.csproj` | The authorized work brief requires Python MinSearch-equivalent fitting, appending, text search, keyword filtering, field boosts, and result limits. The current local `rag_v2.ipynb` has no `minsearch` reference, so this unit establishes the requested library contract before later lesson wiring. | Provider-neutral search contracts, deterministic in-memory implementation, and a no-package executable assertion harness. |
| ODD-003b | Authorized RAG v2 file-based migration → `dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` | The source notebook demonstrates Markdown/MDX loading, YAML frontmatter parsing, and preparation for indexing. This integration replaces remote repository ingestion with a local directory so retrieval is safe and reproducible without network access. | Standalone local lexical-retrieval lesson using `IMinSearchIndex`. |
| ODD-003c | Authorized RAG v2 GitHub ingestion → `dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` | The earlier manual .NET lesson already establishes the required `HttpClient`/in-memory `ZipArchive`/frontmatter parsing behavior for the source repository. The retrieval lesson must offer that complete path rather than only a local shortcut. | Standalone retrieval lesson with default `evidentlyai/docs` `main` GitHub ZIP ingestion and an explicit deterministic `--local` mode. |
| ODD-004a | Authorized reusable-utilities refactor → `dotnet/utilities/DocumentIngestion/DocumentIngestion.csproj` and `dotnet/utilities/DocumentIngestion.Tests/DocumentIngestion.Tests.csproj` | RAG v1 and RAG v2 duplicate provider-bound Markdown ingestion, frontmatter parsing, and document mapping. The reusable boundary must not contain concrete owner, repository, revision, model, or course values. | Provider-neutral immutable document/source contracts, local recursive Markdown source, caller-configured GitHub ZIP source, parser/mapping utilities, and zero-package assertions. |
| ODD-004b | Authorized RAG v1 utility migration → `dotnet/01-Foundation/02-rag/rag_v1_dotnet.cs` | ODD-004a established provider-neutral GitHub ZIP source, Markdown path normalization, basic frontmatter parsing, and generic mapping contracts. RAG v1 retains the concrete `evidentlyai/docs` `main` lesson example and source/entry narration. | Standalone RAG ingestion lesson referencing `DocumentIngestion` through `#:project` with no duplicated ZIP parsing, filtering, path, or frontmatter code. |
| ODD-004c | Authorized RAG v2 utility migration → `dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` | ODD-004a established provider-neutral local/GitHub sources, Markdown path normalization, basic frontmatter parsing, and generic mapping contracts; RAG v2 already owns MinSearch-specific retrieval wiring. | Standalone retrieval lesson referencing `DocumentIngestion` and `MinSearch` through `#:project`, with no duplicated document-ingestion logic. |
| ODD-005a | Authorized structured JSON streaming utility → `dotnet/utilities/StreamingJson/StreamingJson.csproj` and `dotnet/utilities/StreamingJson.Tests/StreamingJson.Tests.csproj` | The structured-stream lesson needs JAXN-equivalent incremental JSON event handling while preserving provider-, model-, schema-, and prompt-neutral reusable contracts. | SAX-style parser and zero-package executable assertions; intended consumer: the structured JSON streaming lesson. |
| ODD-005b | Authorized structured RAG support utility → `dotnet/utilities/RagSupport/RagSupport.csproj` and `dotnet/utilities/RagSupport.Tests/RagSupport.Tests.csproj` | The populated structured RAG lesson needs reusable deterministic character-window chunking and retrieval-context rendering while preserving provider, model, repository, DocumentIngestion, MinSearch, StreamingJson, prompt, and schema neutrality. | Immutable chunk/retrieval contracts, caller-configured chunking and formatting, and a zero-package executable assertion harness. |

## Acceptance Criteria

### ODD-001

- `openai_v1_dotnet.cs` is a .NET 10 file app with current verified Agent Framework package syntax.
- The lesson demonstrates basic and message calls, metadata/usage and cost calculation when supported, streaming, instructions, and stateless/stateful history.
- The root `.env` is loaded without exposing secret values.
- Non-secret compilation or syntax validation is observed where feasible; live model execution is explicitly skipped if it would consume usage or credentials are unavailable.
- This document records observed verification evidence before completion.

### ODD-002

- The RAG ingestion lesson is independently migrated and verified in a later work unit.
- Its implementation and verification evidence are added before it is marked complete.

### ODD-003a

- `dotnet/utilities/MinSearch/MinSearch.csproj` targets .NET 10 and has no package dependencies or Lucene references.
- Public immutable contracts represent documents, index options (text and keyword fields), queries (text, exact-match filters, per-field boosts, and result limit), search hits, and responses using only standard .NET and project contract types.
- `IMinSearchIndex` supports full replacement through `FitAsync`, incremental insertion through `AppendAsync`, and `SearchAsync`, with an API intentionally shaped like Python MinSearch while remaining backend-neutral and future-Lucene-compatible.
- `InMemoryMinSearchIndex` is thread-safe and deterministic; it tokenizes configured text fields, applies documented TF-IDF/cosine-equivalent scoring and field boosts, honors exact keyword filters and limits, and applies deterministic tie-breaking.
- `dotnet/utilities/MinSearch.Tests/` is a zero-external-dependency executable project referencing the library and validates fit, append, ranking/boosts, filters, limits, empty/no-result behavior, and contract immutability through assertions that return nonzero on failure and explicit success on pass.
- This work unit is intentionally dependency-free: it must not restore packages, invoke APIs, or access remote services.

### ODD-003b

- `rag_v2_dotnet.cs` is a .NET 10 file-based app with the exact relative `#:project ../../utilities/MinSearch/MinSearch.csproj` reference.
- The first CLI argument is validated as an existing local directory; the app reads only `.md` and `.mdx` files, parses basic YAML frontmatter, and retains title, description, content, and filename fields.
- The lesson configures text fields, the `filename` keyword field, a sample boosted text query, an optional exact-match filename filter, and a result limit before calling `IMinSearchIndex.SearchAsync`.
- Output presents provider-neutral ranked results and scores, explains that the public MinSearch contracts exclude Lucene types so a future Lucene.NET backend can substitute the in-memory backend, and identifies the supported local input shape.
- The lesson explicitly limits itself to lexical in-memory retrieval: it does not implement chunking, embeddings, vector search, or answer generation.
- The work unit must not restore NuGet packages, invoke APIs, access remote services, or modify unrelated working-tree files.

### ODD-004a

- `dotnet/utilities/DocumentIngestion/DocumentIngestion.csproj` and `dotnet/utilities/DocumentIngestion.Tests/DocumentIngestion.Tests.csproj` target .NET 10, contain no package dependencies, and do not reference MinSearch.
- Public immutable contracts represent source documents and source configuration; `IDocumentSource` exposes provider-neutral asynchronous document loading.
- `LocalDirectoryDocumentSource` recursively loads only Markdown/MDX files and retains normalized relative paths; `GitHubZipDocumentSource` accepts caller-supplied owner, repository, revision, and `HttpClient` rather than embedding concrete values.
- Markdown/MDX path support is centralized. Archive normalization standardizes separators, rejects traversal paths, and strips a root directory only when a common root is proven across relevant entries.
- The intentionally basic YAML frontmatter parser and generic document mapper remain reusable and do not add a YAML dependency.
- The zero-package executable assertion harness verifies supported paths, frontmatter cases, mapping defaults, ZIP root handling including no-common-root archives, traversal rejection, and local relative paths using only in-memory ZIP fixtures and temporary local directories.
- Accepted scope excludes concrete repository ownership, branch/revision, model, course, retrieval, MinSearch, embeddings, and network execution. Later work units migrate RAG v1 first to these sources/parser/mapper, then RAG v2 to the same APIs while retaining its retrieval-specific wiring.

### ODD-004b

- `rag_v1_dotnet.cs` references `../../utilities/DocumentIngestion/DocumentIngestion.csproj` using the exact `#:project` directive and compiles as a standalone .NET 10 file app.
- The lesson keeps `evidentlyai`, `docs`, and `main` as lesson-local source configuration, creates `GitHubZipDocumentSource` with its `HttpClient`, maps returned `SourceDocument` values through `DocumentMapper`, and consumes the generic `IngestedDocument` contract.
- The lesson preserves source configuration narration, archive/document entry visibility, document count, and its stated ingestion-only scope.
- The lesson removes its direct `ZipArchive`, Markdown filtering, archive-prefix removal, frontmatter parsing, and lesson-specific document record contracts.
- Validation restores and builds the file app without running it, runs `git diff --check`, verifies the tracker mirrors are byte-for-byte identical, and confirms build output resolves the referenced `DocumentIngestion` project. GitHub ingestion and external API calls remain prohibited.

### ODD-004c

- `rag_v2_dotnet.cs` references both `../../utilities/DocumentIngestion/DocumentIngestion.csproj` and `../../utilities/MinSearch/MinSearch.csproj` using exact `#:project` directives and compiles as a standalone .NET 10 file app.
- The lesson keeps `evidentlyai`, `docs`, and `main` as lesson-local default source values; it selects `LocalDirectoryDocumentSource` for documented offline `--local` mode and `GitHubZipDocumentSource` with its own `HttpClient` for default GitHub mode.
- The lesson maps `SourceDocument` values through `DocumentMapper` and retains only its MinSearch-specific conversion to `MinSearchDocument`; it removes duplicated local-directory reading, GitHub ZIP extraction, Markdown filtering, path normalization, frontmatter parsing, and generic ingestion mapping.
- Retrieval retains its text and keyword field configuration, boosted query, optional exact filename filter, result limit, output, lexical-only boundary, and Lucene.NET substitution explanation.
- Validation restores and builds the file app, runs it only in `--local` mode against a temporary Markdown/MDX corpus outside the repository with an exact filename filter, runs `git diff --check`, verifies the tracker mirrors are byte-for-byte identical, and confirms build output resolves both referenced projects. GitHub ingestion and external API calls remain prohibited.

### ODD-005a

- `dotnet/utilities/StreamingJson/StreamingJson.csproj` and `dotnet/utilities/StreamingJson.Tests/StreamingJson.Tests.csproj` target .NET 10, contain no package dependencies, and expose no provider, model, schema, prompt, lesson, repository, or response-shape values.
- The public handler contract provides field-start, incrementally decoded string-content chunks, completed field values, completed array items, document completion, and error/state handling analogous to JAXN's streaming event model.
- The parser repeatedly accepts arbitrary UTF-8 byte chunks, preserves structural state and leftover token bytes, and uses `Utf8JsonReader` for JSON structural validation and completed values while lexically emitting decoded string content before its closing quote.
- Deterministic assertions cover escaped quotes and backslashes, `\\uXXXX` escapes including surrogate pairs, UTF-8 code points split across chunks, nested objects and arrays, one-byte chunk splitting, malformed final input, callback ordering, JAXN-like string-chunk immediacy, and completed array-item events.
- Accepted scope is the reusable parser and assertion harness only. The intended structured JSON streaming lesson may consume the utility later; no model invocation, remote call, schema-specific mapping, or lesson migration occurs in this work unit.

### ODD-005b

- `dotnet/utilities/RagSupport/RagSupport.csproj` and `dotnet/utilities/RagSupport.Tests/RagSupport.Tests.csproj` target .NET 10, contain no package dependencies, and expose no concrete provider, model, repository, DocumentIngestion, MinSearch, StreamingJson, lesson, prompt, or schema values.
- Public immutable contracts represent chunks with chunk ID, source identifier, ordinal, text, and provenance, plus caller-provided retrieval records suitable for deterministic context serialization.
- Character-window chunking accepts caller-provided positive size and step values, validates invalid configuration, handles empty and short inputs, preserves source provenance, and generates stable IDs from deterministic input values.
- Context formatting serializes caller-provided retrieval records to deterministic JSON with stable ordering and JSON escaping, then renders a caller-supplied question/context template without embedding prompt text or concrete values.
- The zero-package executable assertion harness covers window boundaries, empty and short input, stable IDs and provenance, invalid configuration, deterministic JSON/context ordering, escaping, and absence of retained external dependencies.
- Accepted scope is the reusable library and assertion harness only. No lesson migration, model invocation, remote call, concrete prompt, schema-specific mapping, or dependency on other utilities occurs in this work unit.

### ODD-003c

- Default input is the GitHub ZIP archive for `evidentlyai/docs` on `main`, downloaded with `HttpClient` into memory and opened with `ZipArchive`.
- The lesson lists relevant archive entries, reads only `.md` and `.mdx` entries, removes the GitHub archive prefix, parses basic YAML frontmatter, and maps title, description, content, and filename into `MinSearchDocument` fields.
- `--local <directory>` remains a documented deterministic offline mode; the default GitHub mode documents optional owner, repository, and branch overrides without hard-coding a one-off workflow.
- Retrieval retains configured text fields, the `filename` keyword filter, boosted query, result limit, provider-neutral output, and Lucene-substitution explanation.
- Output clearly states that GitHub ingestion requires network access and local mode does not.
- Validation must not execute a GitHub download or API call and must not restore packages; run `git diff --check` and only attempt `dotnet build --no-restore dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` when local assets permit.

## TDD and Delivery Strategy

- **TDD mode:** unknown/no runner; use ordinary functional checks suitable for standalone file apps.
- **Review/delivery strategy:** `ask-on-risk`.
- **Work-unit boundary:** ODD-001 is one reviewable commit containing only this document and the OpenAI .NET app.
- **Rollback boundary:** remove `odd/tasks/migrate-rag-dotnet.md` and `dotnet/01-Foundation/02-rag/openai_v1_dotnet.cs`; no unrelated behavior is affected.
- **ODD-002 work-unit boundary:** one reviewable commit containing only this document and `dotnet/01-Foundation/02-rag/rag_v1_dotnet.cs`.
- **ODD-002 rollback boundary:** remove `dotnet/01-Foundation/02-rag/rag_v1_dotnet.cs` and revert only the ODD-002 evidence in this document; no unrelated behavior is affected.
- **ODD-003a work-unit boundary:** one reviewable commit containing only `dotnet/utilities/MinSearch/`, `dotnet/utilities/MinSearch.Tests/`, this document, and its local mirror.
- **ODD-003a validation plan:** run the assertion executable using `dotnet run --project dotnet/utilities/MinSearch.Tests/MinSearch.Tests.csproj --no-restore` only if local assets permit a no-network build; otherwise record that compilation is pending authorization for NuGet restore. Run `git diff --check` and verify the two tracking files remain identical.
- **ODD-003a rollback boundary:** remove `dotnet/utilities/MinSearch/` and `dotnet/utilities/MinSearch.Tests/`, then revert only ODD-003a evidence in this document and its mirror; existing RAG lessons and unrelated working-tree changes remain untouched.
- **ODD-003b #:project integration decision:** reference the local MinSearch project directly from the file-based lesson, using `#:project ../../utilities/MinSearch/MinSearch.csproj`. The lesson depends only on `IMinSearchIndex` and its public contracts, not on Lucene or a provider-specific backend.
- **ODD-003b validation plan:** run `git diff --check`; compare this tracker byte-for-byte with its mirror; and run `dotnet build --no-restore dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` plus a local `dotnet run --no-restore` scenario only if required local assets exist. Do not restore packages, call remote services, or execute APIs.
- **ODD-003b rollback boundary:** remove `dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` and revert only ODD-003b evidence in this document and its mirror. The existing MinSearch library, prior RAG lessons, and unrelated working-tree changes remain untouched.
- **ODD-003b known runtime limitation:** this environment has no local restore assets for the file-based project or MinSearch dependency. A `--no-restore` build/run may stop before compilation with `NETSDK1004`; no restore will be attempted under this authorization.
- **ODD-003c source mode decision:** default to complete GitHub ZIP ingestion for parity with the manual source lesson; use `--local <directory>` only when deterministic offline input is required. Optional `--owner`, `--repo`, and `--branch` values override the default source.
- **ODD-003c validation plan:** inspect the implementation without running GitHub mode; run `git diff --check`; verify both tracker mirrors are identical; and attempt `dotnet build --no-restore dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` only when local assets permit. Do not restore packages or make remote calls.
- **ODD-003c rollback boundary:** revert only `dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` and ODD-003c evidence in both tracker mirrors. Existing MinSearch code, prior RAG lessons, and unrelated working-tree changes remain untouched.
- **ODD-004a accepted scope:** add only `dotnet/utilities/DocumentIngestion/`, `dotnet/utilities/DocumentIngestion.Tests/`, and ODD-004a tracker evidence. No RAG lesson migration occurs in this work unit; no concrete owner, repository, revision, model, course, or source URL is embedded in library contracts or defaults.
- **ODD-004a validation plan:** restore, build, and run the zero-package assertion harness; run `git diff --check`; and verify both tracker mirrors are byte-for-byte identical. Assertions use only in-memory ZIP data and temporary local directories. Do not execute GitHub downloads or external API calls.
- **ODD-004a rollback boundary:** remove only `dotnet/utilities/DocumentIngestion/`, `dotnet/utilities/DocumentIngestion.Tests/`, and ODD-004a tracker evidence in both mirrors. Existing RAG v1, RAG v2, MinSearch, and unrelated working-tree changes remain untouched.
- **ODD-004a migration sequence:** a later RAG v1 work unit replaces its direct ingestion/parser/mapping code with `DocumentIngestion`; a subsequent RAG v2 work unit replaces its duplicate local/GitHub ZIP ingestion/parser/mapping code while preserving its MinSearch retrieval behavior. Each migration is independently validated and committed.
- **ODD-004a review size exception:** this cohesive library, test-harness, and tracker work unit is 472 changed lines. It intentionally keeps assertions and verification evidence with the implementation rather than splitting behavior from its tests.
- **ODD-004b work-unit boundary:** one reviewable commit contains only `dotnet/01-Foundation/02-rag/rag_v1_dotnet.cs` and ODD-004b evidence in both tracker mirrors.
- **ODD-004b validation plan:** restore and build `dotnet/01-Foundation/02-rag/rag_v1_dotnet.cs` without `dotnet run`; run `git diff --check`; and compare both trackers byte-for-byte. Confirm build output includes `DocumentIngestion.dll`, proving the exact `#:project` reference resolved. Do not invoke the GitHub document source, GitHub APIs, or external API calls.
- **ODD-004b rollback boundary:** revert only `dotnet/01-Foundation/02-rag/rag_v1_dotnet.cs` and ODD-004b evidence in both tracker mirrors. The reusable utility, RAG v2, previous work units, and unrelated working-tree changes remain untouched.
- **ODD-004c work-unit boundary:** one reviewable commit contains only `dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` and ODD-004c evidence in both tracker mirrors.
- **ODD-004c validation plan:** restore and build `dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs`; run it only with `--local` against a temporary Markdown/MDX corpus outside the repository and an exact filename filter; run `git diff --check`; compare both trackers byte-for-byte; and confirm build output includes both `DocumentIngestion.dll` and `MinSearch.dll`. Do not invoke GitHub document ingestion or external API calls.
- **ODD-004c rollback boundary:** revert only `dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` and ODD-004c evidence in both tracker mirrors. The reusable utility, MinSearch, RAG v1, previous work units, and unrelated working-tree changes remain untouched.
- **ODD-005a JAXN parity contract:** provide event-oriented, incremental decoded string delivery before closing quotes, completed scalar/field and array-item notifications, document completion, and deterministic parser errors. This is behavioral parity for structured JSON streaming, not a dependency or API clone of JAXN.
- **ODD-005a validation plan:** restore, build, and run the zero-package assertion harness; run `git diff --check`; verify the two tracker mirrors are byte-for-byte identical; and do not invoke models, GitHub, or external APIs.
- **ODD-005a rollback boundary:** remove only `dotnet/utilities/StreamingJson/`, `dotnet/utilities/StreamingJson.Tests/`, and ODD-005a evidence in both tracker mirrors. Existing utilities, lessons, and unrelated working-tree changes remain untouched.
- **ODD-005a review size exception:** this cohesive parser, assertion harness, and tracker work unit exceeds the 400-line review budget. The lexical state machine and deterministic assertions remain together because separating behavior from its verification would create an incomplete work unit.
- **ODD-005b work-unit boundary:** one reviewable commit contains only `dotnet/utilities/RagSupport/`, `dotnet/utilities/RagSupport.Tests/`, and ODD-005b evidence in both tracker mirrors.
- **ODD-005b validation plan:** restore, build, and run the zero-package assertion harness; run `git diff --check`; verify the tracker mirrors are byte-for-byte identical; inspect project files and source references for prohibited external utility/package dependencies; and do not invoke models, GitHub, or external APIs.
- **ODD-005b rollback boundary:** remove only `dotnet/utilities/RagSupport/`, `dotnet/utilities/RagSupport.Tests/`, and ODD-005b evidence in both tracker mirrors. Existing utilities, lessons, and unrelated working-tree changes remain untouched.

## ODD-005a Bounded Correctness/Coverage Correction

### Scope and Validation Plan

- Add deterministic assertions for callback order across multi-level nested arrays and for malformed final inputs split across chunks: incomplete UTF-8, incomplete Unicode escape, and unpaired surrogate.
- Correct only the minimal parser code required if those assertions expose incorrect behavior. Preserve provider, model, schema, prompt, lesson, repository, and response-shape neutrality.
- Restore, build, and run `dotnet/utilities/StreamingJson.Tests/StreamingJson.Tests.csproj`; run `git diff --check`; compare these tracker mirrors byte-for-byte; and do not invoke models, GitHub, or external services.

### Correction Rollback Boundary

- Revert only `dotnet/utilities/StreamingJson/`, `dotnet/utilities/StreamingJson.Tests/`, and this correction evidence in both tracker mirrors. Existing utilities, lessons, prior ODD-005a behavior, and unrelated working-tree changes remain untouched.

## Progress

| ID | Status | Evidence |
| --- | --- | --- |
| ODD-001 | Complete (compile verification) | Corrected and restored `openai_v1_dotnet.cs` with `Microsoft.Agents.AI.OpenAI` `1.22.0`; compile-only verification passed without a live model invocation. |
| ODD-002 | Complete (static verification) | Implemented `rag_v1_dotnet.cs`; source review, whitespace validation, and a no-restore build attempt are recorded below. Remote execution remains intentionally unverified. |
| ODD-003a | Complete (compile and assertion verification) | Corrected the incompatible collection-expression fallbacks with `ReadOnlyDictionary` values; restore and all eight executable assertions passed. |
| ODD-003b | Complete (build and local runtime verification) | The file app built and returned the one exact-filtered result from a temporary local Markdown/MDX corpus; `#:project` resolved the MinSearch project. |
| ODD-003c | Complete (static validation) | Implemented default GitHub ZIP ingestion plus explicit offline local mode; remote execution and no-restore compilation were intentionally skipped because they were outside the available validation boundary. |
| ODD-004a | Complete (restore, build, and assertion verification) | The dependency-free library and seven in-memory/temporary-directory assertions passed; no GitHub download or external API call was executed. |
| ODD-004b | Complete (restore and build verification) | The RAG v1 file app restored and built without execution; build output emitted `DocumentIngestion.dll`, confirming the exact `#:project` reference resolved. |
| ODD-004c | Complete (restore, build, and local runtime verification) | The RAG v2 file app restored and built with both project references, then returned one exact-filtered result from a temporary offline Markdown/MDX corpus; GitHub mode was not invoked. |
| ODD-005a | Complete (restore, build, and assertion verification) | The dependency-free SAX-style parser and all twelve deterministic assertions passed without model, GitHub, or external API calls. |
| ODD-005b | Complete (restore, build, and assertion verification) | The dependency-free chunking/context library and all seven deterministic assertions passed without model, GitHub, or external API calls. |

## ODD-001 Bounded Correction

### Prior Evidence

- The original ODD-001 work unit was committed as `3cbd239 feat(rag): add OpenAI .NET lesson` using `Microsoft.Agents.AI.OpenAI` `1.13.0`.
- Static validation previously stopped before compilation because the file-app restore assets were absent; no packages were restored.
- Source review identified the missing `using OpenAI.Responses;` required for the Responses `AsAIAgent` extension, and root lookup only accepted `.git` directories even though worktrees use `.git` files.
- No model request was made; live execution remains outside this correction's authorization.

### Correction Scope and Verification Plan

- Add the Responses namespace import, update to the current stable `Microsoft.Agents.AI.OpenAI` package verified from NuGet during the authorized restore, and make only API-compatible source corrections required by that restored package.
- Treat a repository `.git` marker as either a file or directory; relabel `RawRepresentation` as raw provider response metadata.
- Retain the final streamed response/update metadata and run the existing usage and illustrative cost calculation against the restored framework API.
- Run `dotnet restore` and a compile-only `dotnet build` or equivalent that does not invoke the model; do not run the RAG app or any live OpenAI prompt.
- Run `git diff --check`, record exact command outcomes, package version, and skipped live invocation reason below; record the correction commit identity in the delivery report.

### Correction Rollback Boundary

- Revert only `dotnet/01-Foundation/02-rag/openai_v1_dotnet.cs`, this correction section, and its mirror at `odd/migrate-rag-dotnet/tasks/migrate-rag-dotnet.md`; ODD-002 and unrelated working-tree changes remain untouched.

## ODD-003a/003b Bounded Correction

### Confirmed Failure and Scope

- `dotnet` compilation confirmed `CS0019` in `MinSearchQuery`: `exactMatchFilters ?? []` and `fieldBoosts ?? []` cannot infer compatible `IReadOnlyDictionary` types.
- Correct only the public contract fallbacks with explicitly compatible immutable dictionary values. Do not change the backend-neutral API shape, scoring behavior, or the RAG lesson semantics.
- Restore the MinSearch test project, run its assertion executable, build the RAG v2 file app, and run that app against a temporary local Markdown/MDX corpus with an exact filename filter. No external APIs will be invoked.

### Correction Rollback Boundary

- Revert only `dotnet/utilities/MinSearch/MinSearch.cs` and this correction evidence in both tracker mirrors. The RAG v2 lesson, prior work units, and unrelated working-tree changes remain untouched.

## Verification Evidence

- ODD-001 uses `Microsoft.Agents.AI.OpenAI` version `1.13.0` via a .NET 10 `#:package` directive. The official Microsoft Learn OpenAI integration page documents `OpenAIClient`, `GetResponsesClient()`, and `AsAIAgent(...)` as the recommended Responses API route: <https://learn.microsoft.com/agent-framework/integrations/by-component/model-providers/openai>.
- The official Microsoft Learn API reference reports `Microsoft.Agents.AI.OpenAI` version `1.13.0` for the OpenAI chat-client extensions: <https://learn.microsoft.com/dotnet/api/openai.chat.openaichatclientextensions?view=agent-framework-dotnet-latest>.
- `git diff --check` completed successfully with no whitespace errors.
- `dotnet run --no-restore dotnet/01-Foundation/02-rag/openai_v1_dotnet.cs` reached the .NET 10 SDK and failed before compilation because `project.assets.json` is absent. Restoring packages would require downloading the un-cached NuGet dependency and was not performed.
- Live model execution was skipped: it would consume API usage and may require credentials. No credential values were read or logged.
- ODD-002 uses only built-in .NET APIs: `HttpClient`, `ZipArchive`, `StreamReader`, and a transparent frontmatter parser. It does not add a YAML package.
- `dotnet --version` reported `10.0.401`.
- After marking the new file intent-to-add, `git diff --check -- odd/tasks/migrate-rag-dotnet.md dotnet/01-Foundation/02-rag/rag_v1_dotnet.cs` completed successfully with no whitespace errors.
- `dotnet build --no-restore dotnet/01-Foundation/02-rag/rag_v1_dotnet.cs` reached the .NET 10 SDK and failed before compilation because the file-app temporary `project.assets.json` is absent. Restoring was not performed.
- Remote execution was skipped because the app downloads the GitHub archive and remote operations were not authorized. No secrets were read or logged.
- NuGet metadata verification with `dotnet package search Microsoft.Agents.AI.OpenAI --source https://api.nuget.org/v3/index.json --take 5` reported stable version `1.22.0`.
- `dotnet restore dotnet/01-Foundation/02-rag/openai_v1_dotnet.cs` succeeded and restored the generated file-app project in 7.22 seconds.
- The correction adds `using OpenAI.Responses;`, suppresses the SDK's required experimental Responses diagnostic locally, converts the streamed update sequence with `ToAgentResponseAsync()`, and passes the final aggregated streamed response to the existing metadata, usage, and illustrative-cost formatter.
- The corrected repository-root lookup accepts either a `.git` directory or a `.git` worktree file; `RawRepresentation` is labeled as raw provider response metadata.
- `dotnet build --no-restore dotnet/01-Foundation/02-rag/openai_v1_dotnet.cs` succeeded and emitted the file-app DLL. No `dotnet run` command was issued: a live prompt could consume usage and credentials, so model invocation remains intentionally skipped.
- `git diff --check -- dotnet/01-Foundation/02-rag/openai_v1_dotnet.cs odd/tasks/migrate-rag-dotnet.md odd/migrate-rag-dotnet/tasks/migrate-rag-dotnet.md` completed successfully with no whitespace errors; the tracking document and its mirror were byte-for-byte identical before staging.
- ODD-003a adds `dotnet/utilities/MinSearch/MinSearch.csproj` and `dotnet/utilities/MinSearch.Tests/MinSearch.Tests.csproj`, both targeting `net10.0`, with no `PackageReference` items or Lucene references. The test project uses only a project reference to the library.
- Source review confirms that public contracts expose only standard .NET collection/task/cancellation types and MinSearch contracts. `InMemoryMinSearchIndex` uses a reader-writer lock, immutable snapshots, tokenized text fields, per-field TF-IDF cosine scoring, ordinal keyword filters, ordinal document-id tie-breaking, boosts, and limits.
- `dotnet run --project dotnet/utilities/MinSearch.Tests/MinSearch.Tests.csproj --no-restore` reached the .NET 10 SDK and failed with `NETSDK1004` because `dotnet/utilities/MinSearch.Tests/obj/project.assets.json` is absent. No package restore was attempted, so compilation and assertion execution remain pending authorization for a NuGet restore.
- The executable assertion harness returns `1` and writes `FAIL:` for an assertion exception, or returns `0` and writes `SUCCESS:` after validating fit, append, boosts, filters, limits, deterministic ties, empty/no-result behavior, and contract immutability.
- The local `rag_v2.ipynb` was inspected and contains no `minsearch` reference; the library contract follows the authorized migration brief rather than claiming an unobserved notebook call. No API calls or remote services were accessed.
- ODD-003b adds `dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` as a .NET 10 file-based app with the exact local project reference `#:project ../../utilities/MinSearch/MinSearch.csproj`. It loads recursively discovered local `.md` and `.mdx` files only, parses basic YAML frontmatter, and maps title, description, content, and normalized relative filename into `MinSearchDocument` fields.
- The lesson configures `title`, `description`, and `content` as text fields; `filename` as a keyword field; a boosted lexical query; an optional exact filename filter; and a result limit of five. It calls `IMinSearchIndex.FitAsync` and `SearchAsync`, then prints provider-neutral scores and ranked results.
- The lesson output states that public MinSearch contracts exclude Lucene types so a future Lucene.NET backend can replace the in-memory implementation, and that its scope excludes chunking, embeddings, vector search, and answer generation.
- `git diff --check -- odd/tasks/migrate-rag-dotnet.md odd/migrate-rag-dotnet/tasks/migrate-rag-dotnet.md dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` completed successfully. A SHA-256 comparison confirmed the two tracking files were byte-for-byte identical.
- `dotnet --version` reported `10.0.401`. `dotnet build --no-restore dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` reached the SDK and stopped with `NETSDK1004` because the generated file-app `project.assets.json` is absent under the local temporary `dotnet/runfile` directory. The MinSearch project also has no local `obj/project.assets.json`. No restore was attempted, so compilation and a deterministic local run remain pending authorization for restore assets.
- The ODD-003a/003b correction replaced the incompatible collection-expression fallbacks in `MinSearchQuery` with explicitly typed `ReadOnlyDictionary<string, string>` and `ReadOnlyDictionary<string, double>` values. The existing copy methods retain independent immutable public contract values.
- `dotnet restore dotnet/utilities/MinSearch.Tests/MinSearch.Tests.csproj` succeeded; it restored the test project in 95 ms and the MinSearch project in 87 ms.
- `dotnet run --project dotnet/utilities/MinSearch.Tests/MinSearch.Tests.csproj --no-restore` succeeded with all eight assertions passing and `SUCCESS: 8 MinSearch assertions passed.`
- `dotnet build dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` succeeded and emitted the RAG v2 file-app DLL. Its output included the MinSearch DLL, confirming that `#:project ../../utilities/MinSearch/MinSearch.csproj` resolved successfully.
- A temporary corpus outside the repository contained `guides/azure.md` and `other.mdx`. `dotnet run dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs -- <temporary-corpus> Azure guides/azure.md` loaded two documents and returned exactly one hit, `guides/azure.md`; the temporary corpus was removed afterward.
- `git diff --check` completed successfully with no whitespace errors after the correction and validation evidence were recorded.
- The task tracker mirrors were byte-for-byte identical after the correction.
- ODD-003c reuses the manual lesson's built-in `HttpClient`/in-memory `ZipArchive`/UTF-8 `StreamReader` and basic frontmatter-parser pattern. Default execution targets `https://github.com/evidentlyai/docs/archive/refs/heads/main.zip`; archive entries are filtered to Markdown/MDX, displayed, prefix-normalized, and mapped to MinSearch fields.
- ODD-003c keeps `--local <directory>` as deterministic offline input and supports optional `--owner`, `--repo`, and `--branch` GitHub source overrides. The CLI and runtime output explicitly distinguish network-required GitHub mode from local offline mode.
- Source inspection confirms retrieval still configures `title`, `description`, and `content` text fields; `filename` keyword filtering; field boosts; a five-result limit; provider-neutral output; and the Lucene.NET substitution explanation.
- `git diff --check` completed successfully with no whitespace errors after ODD-003c implementation. A repository-wide `project.assets.json` check found no local assets, so `dotnet build --no-restore dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` was not attempted. No package restore, GitHub download, API call, or GitHub-mode application execution occurred.
- The ODD-003c tracker mirrors were updated before source edits and remain byte-for-byte identical.
- ODD-004a adds dependency-free `DocumentIngestion` and `DocumentIngestion.Tests` .NET 10 projects. Its public surface is limited to immutable source/frontmatter/mapped-document contracts, `IDocumentSource`, recursive local and caller-configured GitHub ZIP sources, Markdown/MDX path helpers, archive normalization, the intentionally basic frontmatter parser, and generic mapping. It has no MinSearch reference or concrete owner, repository, revision, model, or course values.
- `dotnet restore dotnet/utilities/DocumentIngestion.Tests/DocumentIngestion.Tests.csproj` succeeded; both projects restored without package dependencies.
- `dotnet build dotnet/utilities/DocumentIngestion.Tests/DocumentIngestion.Tests.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet run --project dotnet/utilities/DocumentIngestion.Tests/DocumentIngestion.Tests.csproj --no-build` succeeded with all seven assertions passing: supported paths, frontmatter cases, mapping defaults, common-root ZIP handling, no-common-root ZIP handling, traversal rejection, and local relative paths.
- ZIP assertions used a custom in-memory `HttpMessageHandler` and in-memory `ZipArchive` fixtures; local assertions used a temporary directory that was removed afterward. No GitHub download or external API call was executed.
- `git diff --check` completed successfully with no whitespace errors. SHA-256 verification confirmed both ODD tracker mirrors are byte-for-byte identical.
- ODD-004b scope, acceptance criteria, validation plan, and rollback boundary were recorded in both tracker mirrors before editing `rag_v1_dotnet.cs`.
- `dotnet restore dotnet/01-Foundation/02-rag/rag_v1_dotnet.cs` succeeded in 1.02 seconds; no GitHub source was executed during restore.
- `dotnet build --no-restore dotnet/01-Foundation/02-rag/rag_v1_dotnet.cs` succeeded and emitted both `DocumentIngestion.dll` and the file-app DLL, confirming `#:project ../../utilities/DocumentIngestion/DocumentIngestion.csproj` resolved.
- No `dotnet run` command was issued. GitHub ZIP ingestion remains intentionally unverified at runtime because it would make a remote document-download request.
- ODD-004c scope, acceptance criteria, validation plan, and rollback boundary were recorded in both tracker mirrors before editing `rag_v2_dotnet.cs`.
- `rag_v2_dotnet.cs` now references both `DocumentIngestion` and `MinSearch`; it selects the reusable local or caller-configured GitHub source, maps `SourceDocument` values with `DocumentMapper`, and retains only the MinSearch-specific field mapping and retrieval wiring.
- `dotnet restore dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` succeeded, restoring the file app and MinSearch project while leaving one of three projects unchanged.
- `dotnet build --no-restore dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs` succeeded and emitted `DocumentIngestion.dll`, `MinSearch.dll`, and the file-app DLL.
- A temporary corpus outside the repository contained `guides/retrieval.md`, `other.mdx`, and an ignored `.txt` file. `dotnet run --no-restore dotnet/01-Foundation/02-rag/rag_v2_dotnet.cs -- --local <temporary-corpus> --query retrieval --filename guides/retrieval.md` loaded two Markdown/MDX documents and returned exactly one hit, `guides/retrieval.md`; the temporary corpus was removed afterward. GitHub mode and external API calls were not invoked.
- ODD-005a tracker scope, JAXN behavioral-parity contract, acceptance criteria, validation plan, rollback boundary, and intended structured JSON streaming lesson consumer were recorded in both mirrors before source edits.
- ODD-005a adds dependency-free `StreamingJson` and `StreamingJson.Tests` .NET 10 projects. Its public handler contracts expose field start, decoded string chunks, completed field values, completed array items, document completion, and parser errors without provider, model, schema, prompt, lesson, repository, or response-shape values.
- The parser preserves incremental lexical and structural state across byte chunks, emits decoded string content before closing quotes, handles escape sequences, Unicode surrogate pairs, and split UTF-8 code points, and uses `Utf8JsonReader` plus `JsonDocument` to validate and materialize completed values. Root composite documents are not retained solely for validation; only active values that require completion events retain their bytes.
- `dotnet restore dotnet/utilities/StreamingJson.Tests/StreamingJson.Tests.csproj` succeeded without package dependencies. `dotnet build dotnet/utilities/StreamingJson.Tests/StreamingJson.Tests.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet run --project dotnet/utilities/StreamingJson.Tests/StreamingJson.Tests.csproj --no-build` succeeded with `SUCCESS: 8 StreamingJson assertions passed.` The assertions cover pre-close string chunk delivery, escaped quotes/backslashes, Unicode surrogate pairs, split UTF-8, nested ordering, completed array items, one-byte chunks, and malformed final input.
- The bounded ODD-005a correction added deterministic coverage for callback ordering through multi-level nested arrays and for split malformed final inputs: incomplete UTF-8, incomplete Unicode escape, and unpaired surrogate. The existing parser behavior satisfied all new assertions; no parser source change was required.
- `dotnet restore dotnet/utilities/StreamingJson.Tests/StreamingJson.Tests.csproj --ignore-failed-sources` reported that all projects were up to date. `dotnet build dotnet/utilities/StreamingJson.Tests/StreamingJson.Tests.csproj --no-restore` succeeded with 0 warnings and 0 errors. `dotnet run --project dotnet/utilities/StreamingJson.Tests/StreamingJson.Tests.csproj --no-build` succeeded with `SUCCESS: 12 StreamingJson assertions passed.`
- `git diff --check -- dotnet/utilities/StreamingJson.Tests/Program.cs odd/tasks/migrate-rag-dotnet.md odd/migrate-rag-dotnet/tasks/migrate-rag-dotnet.md` completed successfully. SHA-256 comparison confirmed the tracker mirrors are byte-for-byte identical. No external service, model, GitHub, or remote API was invoked.
- ODD-005b tracker scope, no-concrete-values constraint, acceptance criteria, validation plan, and rollback boundary were recorded in both mirrors before creating RagSupport source files.
- ODD-005b adds dependency-free `RagSupport` and `RagSupport.Tests` .NET 10 projects. Public contracts provide immutable chunks/provenance and caller-provided retrieval records; the library holds no concrete provider, model, repository, DocumentIngestion, MinSearch, StreamingJson, lesson, prompt, or schema values.
- `dotnet restore dotnet/utilities/RagSupport.Tests/RagSupport.Tests.csproj` succeeded: RagSupport restored in 89 ms and RagSupport.Tests in 99 ms.
- `dotnet build dotnet/utilities/RagSupport.Tests/RagSupport.Tests.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet run --project dotnet/utilities/RagSupport.Tests/RagSupport.Tests.csproj --no-build` succeeded with `SUCCESS: 7 RagSupport assertions passed.` The harness covers deterministic windows, empty/short input, stable IDs/provenance, invalid configuration, deterministic JSON ordering, escaping/template rendering, and library reference neutrality.
- No model, GitHub, or external API call was executed. The only authorized network-capable operation was NuGet restoration.

## ODD-005c — RAG v3 and v4 .NET Ingestion Lessons

### Scope and Pedagogical Boundary

- Migrate `01-Foundation/02-rag/rag_v3.ipynb` to `dotnet/01-Foundation/02-rag/rag_v3_dotnet.cs` and `01-Foundation/02-rag/rag_v4.ipynb` to `dotnet/01-Foundation/02-rag/rag_v4_dotnet.cs` as separate .NET 10 file apps.
- Preserve both targets because they teach two distinct entry points to the same preparation outcome: v3 explicitly walks from the raw GitHub ZIP source through frontmatter parsing before comparing GitSource; v4 begins with the GitSource workflow and then parses its returned document records. The .NET lessons do not invent an external GitSource equivalent; both use the existing native `GitHubZipDocumentSource` utility and explain that deliberate substitution.
- Each lesson has its own concrete `evidentlyai`, `docs`, and `main` configuration, owns its `HttpClient`, and constructs `GitHubZipDocumentSource` with that client. Concrete source values remain outside `DocumentIngestion`.
- Each lesson references exactly `#:project ../../utilities/DocumentIngestion/DocumentIngestion.csproj`, reports the configured source and returned source-document count, maps documents with `DocumentMapper`, and shows metadata-parsed records including title, description, content preview, and filename.
- Both lessons cover ingestion and preparation only. They must explicitly exclude retrieval, indexing, embeddings, and generation.

### Acceptance Criteria

- `rag_v3_dotnet.cs` and `rag_v4_dotnet.cs` compile as independent .NET 10 file apps without project files and resolve the reusable `DocumentIngestion` project through the exact relative directive.
- The v3 lesson retains the raw-source-to-prepared-record teaching sequence; the v4 lesson retains the concise reader-to-parsed-record teaching sequence while making the lack of a .NET GitSource counterpart explicit rather than fabricating one.
- No direct ZIP extraction, Markdown filtering, path normalization, frontmatter parsing, or generic mapping is duplicated in either lesson.
- Restore and build both file apps without executing them. Do not run either lesson in GitHub mode and do not invoke GitHub downloads, GitHub APIs, or external APIs.
- Run `git diff --check`, verify both tracker mirrors are byte-for-byte identical, and confirm build output emits `DocumentIngestion.dll` for both apps.

### Validation and Rollback Plan

- Run `dotnet restore` and `dotnet build --no-restore` for each file app. The runtime harness is intentionally N/A: every normal execution path loads the GitHub archive, and remote execution is prohibited.
- Roll back only `dotnet/01-Foundation/02-rag/rag_v3_dotnet.cs`, `dotnet/01-Foundation/02-rag/rag_v4_dotnet.cs`, and ODD-005c evidence in these two tracker mirrors. The reusable utility, prior lessons, and unrelated working-tree changes remain untouched.

### Status

| ID | Status | Evidence |
| --- | --- | --- |
| ODD-005c | Complete (restore and build verification) | Both standalone file apps restored and built without execution; `DocumentIngestion.dll` was emitted for each. |

### Verification Evidence

- Before source edits, both tracker mirrors and Engram recorded the scope, separate-target rationale, acceptance criteria, validation plan, and rollback boundary.
- `rag_v3_dotnet.cs` preserves the raw source-to-frontmatter-to-mapped-record progression before explaining the original notebook's GitSource comparison. `rag_v4_dotnet.cs` preserves the reader-to-mapped-record progression and explicitly states that no external .NET GitSource equivalent is introduced.
- `dotnet restore dotnet/01-Foundation/02-rag/rag_v3_dotnet.cs` succeeded in 2.33 seconds. `dotnet build --no-restore dotnet/01-Foundation/02-rag/rag_v3_dotnet.cs` succeeded and emitted `DocumentIngestion.dll` and `rag_v3_dotnet.dll`.
- `dotnet restore dotnet/01-Foundation/02-rag/rag_v4_dotnet.cs` succeeded in 406 ms. `dotnet build --no-restore dotnet/01-Foundation/02-rag/rag_v4_dotnet.cs` succeeded and emitted `DocumentIngestion.dll` and `rag_v4_dotnet.dll`.
- No `dotnet run` command was issued. Both normal execution paths use `GitHubZipDocumentSource`, so GitHub downloads, GitHub APIs, and external APIs remained prohibited and uninvoked.

## ODD-005d — Structured Output .NET Lesson

### Scope and Pedagogical Boundary

- Migrate `01-Foundation/02-rag/structuredout_v1.ipynb` to `dotnet/01-Foundation/02-rag/structuredout_v1_dotnet.cs` as a standalone .NET 10 file app.
- Preserve the notebook's non-streaming lesson: define a `CalendarEvent` schema with required `name`, `date`, and `participants`; request a typed parsed result; show the raw JSON response text; and contrast that stable contract with an unstructured text response.
- Use the current Microsoft Agent Framework/OpenAI integration with lesson-local model configuration and the repository-root `.env` loading pattern already used by the OpenAI lesson. Never print environment values or credentials.

### Acceptance Criteria

- The file app uses .NET 10 directives and a verified current `Microsoft.Agents.AI.OpenAI` package/API.
- `CalendarEvent` is a typed output model whose `Name`, `Date`, and `Participants` properties are required by its generated JSON schema.
- The structured invocation uses `RunAsync<CalendarEvent>` and presents both `response.Result` and `response.Text`; a separate text invocation demonstrates why an unstructured response has no equivalent typed contract.
- Restore and build without executing the app. Do not invoke a model, API, or provider; do not read or log credential values.
- Run `git diff --check`, verify these tracker mirrors are byte-for-byte identical, and record exact package/API and validation evidence before marking the task complete.

### Validation and Rollback Plan

- Run `dotnet restore dotnet/01-Foundation/02-rag/structuredout_v1_dotnet.cs` and `dotnet build --no-restore dotnet/01-Foundation/02-rag/structuredout_v1_dotnet.cs`. The runtime harness is intentionally N/A because every lesson path invokes a live model.
- Roll back only `dotnet/01-Foundation/02-rag/structuredout_v1_dotnet.cs` and ODD-005d evidence in these two tracker mirrors. Earlier lessons, reusable utilities, and unrelated working-tree changes remain untouched.

### Status

| ID | Status | Evidence |
| --- | --- | --- |
| ODD-005d | Complete (restore and build verification) | The standalone file app restored and built without execution. |

### Verification Evidence

- Before source edits, both tracker mirrors and Engram recorded ODD-005d scope, required typed schema, validation plan, and rollback boundary.
- `CalendarEvent` uses required `Name`, `Date`, and `Participants` properties with `name`, `date`, and `participants` JSON names. `RunAsync<CalendarEvent>` exposes `response.Result`, while `response.Text` exposes the raw JSON text. A separate non-generic invocation shows the unstructured-text boundary.
- Microsoft Agent Framework structured-output guidance documents `AIAgent.RunAsync<T>` for a compile-time-known typed result and `AgentResponse<T>.Text` for the raw JSON. Its OpenAI integration documents `OpenAIClient`, `GetResponsesClient()`, and `AsAIAgent(...)`, with the Responses client recommended for new integrations.
- `dotnet restore dotnet/01-Foundation/02-rag/structuredout_v1_dotnet.cs` succeeded, restoring the generated file-app project in 412 ms. `dotnet build --no-restore dotnet/01-Foundation/02-rag/structuredout_v1_dotnet.cs` succeeded and emitted `structuredout_v1_dotnet.dll` with `Microsoft.Agents.AI.OpenAI` `1.22.0`.
- `git diff --check -- dotnet/01-Foundation/02-rag/structuredout_v1_dotnet.cs odd/tasks/migrate-rag-dotnet.md odd/migrate-rag-dotnet/tasks/migrate-rag-dotnet.md` completed successfully. SHA-256 verification confirmed that these tracker mirrors are byte-for-byte identical.
- No `dotnet run` command was issued. Every demonstration request calls a live model and may consume usage; credentials were not read or logged.

## ODD-005e-c — Streaming Structured RAG Compile-Quality Correction

### Scope and Pedagogical Boundary

- Correct only `dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs` to eliminate compiler/analyzer noise without changing the notebook lesson flow.
- Keep the final structured-RAG streaming scenario defined but uninvoked; do not introduce a command-line switch, model request, document download, or other execution path for it.
- Replace reflection-based generic JSON deserialization with source-generated `JsonTypeInfo` deserialization for the typed article and RAG results, consistent with file-based app/AOT guidance.

### Validation and Rollback Plan

- Run `dotnet build dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs`, confirming zero warnings and zero errors; then run `dotnet run --no-build dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs -- --offline-fixture`.
- Run `git diff --check` for the lesson and both tracker mirrors, and verify the two tracker files are byte-for-byte identical. Do not execute normal lesson mode, model/API requests, GitHub downloads, or external APIs.
- Roll back only the bounded source correction and this ODD-005e-c section in both tracker mirrors. ODD-005e, other lessons, utilities, and unrelated working-tree changes remain untouched.

### Status

| ID | Status | Evidence |
| --- | --- | --- |
| ODD-005e-c | Complete (warning-free build and offline fixture) | The final RAG scenario remains defined but uninvoked; source-generated JSON metadata is exercised locally. |

### Verification Evidence

- Before source edits, both tracker mirrors and Engram topic `odd/migrate-rag-dotnet/tasks` recorded the correction scope, validation plan, and rollback boundary.
- The intentional non-execution boundary is preserved: `StreamRagAsync`, index construction, and retrieval mapping remain defined under a narrowly scoped `CS8321` suppression, with no new invocation or command-line execution path. The unused top-level `ragSource` declaration was removed.
- `StructuredRagJsonContext` supplies source-generated `JsonTypeInfo` for `ArticleResponse` and `AnswerResponse`; both streaming methods and the offline fixture use that metadata rather than `JsonSerializer.Deserialize<T>`.
- `dotnet build dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs` completed with zero warnings and zero errors. `dotnet run --no-build dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs -- --offline-fixture` printed `SUCCESS: offline StreamingJson article and RAG handler fixture passed.`
- `git diff --check -- dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs odd/tasks/migrate-rag-dotnet.md odd/migrate-rag-dotnet/tasks/migrate-rag-dotnet.md` completed successfully, and SHA-256 comparison confirmed these tracker mirrors are byte-for-byte identical.
- No normal lesson execution, model/API request, GitHub download, or external API call was made.

## ODD-005e — Streaming Structured RAG .NET Lesson

### Scope and Pedagogical Boundary

- Migrate `01-Foundation/02-rag/structuredrag_stream_v1.ipynb` to `dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs` as a standalone .NET 10 file app.
- Preserve the notebook's actual sections: text streaming, typed article-schema streaming, JAXN-equivalent incremental JSON rendering, document ingestion/chunking/indexing, and typed RAG response handlers. The final RAG-stream scenario is defined but not invoked by the notebook, so this lesson must not claim it was invoked.
- Reference `DocumentIngestion`, `MinSearch`, `RagSupport`, and `StreamingJson` through `#:project`. Keep concrete prompts, source defaults, model values, and schemas lesson-local.

### Acceptance Criteria

- The file app uses the verified Agent Framework/OpenAI configuration pattern and repository-root `.env` loading without printing credential values.
- Text and typed article streaming use the Agent Framework/OpenAI streaming API; structured JSON deltas are fed to `StreamingJson` so string chunks render early and completed fields and array items render safely.
- The lesson configures reusable document ingestion, 3000-size/1500-step chunking, lexical retrieval, and deterministic context formatting for the defined typed RAG-stream function.
- A deterministic offline fixture validates parser/handler integration without model, GitHub, or API requests.
- Restore and build the file app; run only its offline fixture; run `git diff --check`; verify the tracker mirrors are byte-for-byte identical; do not invoke a model, GitHub download, or external API.

### Validation and Rollback Plan

- Run `dotnet restore dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs`, `dotnet build --no-restore dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs`, and `dotnet run --no-restore dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs -- --offline-fixture`.
- Roll back only `dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs` and ODD-005e evidence in these two tracker mirrors. Earlier lessons, reusable utilities, and unrelated working-tree changes remain untouched.

### Status

| ID | Status | Evidence |
| --- | --- | --- |
| ODD-005e | Complete (restore, build, and offline fixture verification) | The file app restored and built; the deterministic streaming parser/handler fixture passed without a model or document download. |

### Verification Evidence

- Before source edits, both tracker mirrors and Engram recorded ODD-005e scope, acceptance criteria, validation, and rollback boundary.
- The lesson references `DocumentIngestion`, `MinSearch`, `RagSupport`, and `StreamingJson` through `#:project`; it keeps concrete source, model, prompt, and schema values local.
- `dotnet restore dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs` succeeded. `dotnet build --no-restore dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs` succeeded and resolved all four local utility projects.
- `dotnet run --no-build --no-restore dotnet/01-Foundation/02-rag/structuredrag_stream_v1_dotnet.cs -- --offline-fixture` succeeded with `SUCCESS: offline StreamingJson article and RAG handler fixture passed.` It feeds one-byte JSON chunks and verifies early article/RAG string rendering plus completed field and array-item output.
- No normal `dotnet run` was issued. Normal text/article streaming would invoke a live model, and the defined RAG-stream scenario would download documents; neither model/API calls nor GitHub downloads were made.

## ODD-005f — Non-Streaming Structured RAG .NET Lesson

### Scope and Pedagogical Boundary

- Migrate `01-Foundation/02-rag/structuredrag_v1.ipynb` to `dotnet/01-Foundation/02-rag/structuredrag_v1_dotnet.cs` as a standalone .NET 10 file app.
- Preserve the actual flow: configurable ingestion source, 3000-size/1500-step chunks, lexical top-five retrieval, deterministic JSON context/prompt, unstructured RAG call, typed structured-schema progression, and the final found/not-found union.
- Reference `DocumentIngestion`, `MinSearch`, and `RagSupport` through `#:project`. Keep concrete source defaults, models, prompts, and schemas lesson-local. The notebook describes retry behavior but does not implement it; this lesson must not invent retries.

### Acceptance Criteria

- The file app uses generic utilities for ingestion, chunking, retrieval, and context formatting while preserving the lesson-local RAG prompts and typed schemas.
- The unstructured call and typed calls use the verified Agent Framework/OpenAI configuration pattern; the final union validates that exactly one found/not-found branch is present.
- A deterministic local-only fixture validates ingestion, chunking, lexical retrieval, and deterministic context construction without model, GitHub, or API requests.
- Restore and build the file app; run only its local fixture; run `git diff --check`; verify the tracker mirrors are byte-for-byte identical; do not invoke a model, GitHub download, or external API.

### Validation and Rollback Plan

- Run `dotnet restore dotnet/01-Foundation/02-rag/structuredrag_v1_dotnet.cs`, `dotnet build --no-restore dotnet/01-Foundation/02-rag/structuredrag_v1_dotnet.cs`, and `dotnet run --no-build --no-restore dotnet/01-Foundation/02-rag/structuredrag_v1_dotnet.cs -- --offline-fixture`.
- Roll back only `dotnet/01-Foundation/02-rag/structuredrag_v1_dotnet.cs` and ODD-005f evidence in these two tracker mirrors. Earlier lessons, reusable utilities, and unrelated working-tree changes remain untouched.

### Status

| ID | Status | Evidence |
| --- | --- | --- |
| ODD-005f | Complete (restore, build, and offline fixture verification) | The file app restored and built; the deterministic local ingestion-to-context fixture passed without a model or document download. |

### Verification Evidence

- Before source edits, both tracker mirrors and Engram recorded ODD-005f scope, acceptance criteria, validation, and rollback boundary.
- The lesson references `DocumentIngestion`, `MinSearch`, and `RagSupport` through `#:project`; it preserves configurable local/GitHub ingestion, 3000/1500 chunking, lexical top-five retrieval, deterministic context rendering, unstructured RAG, typed RAG progression, and the final found/not-found union without adding retries.
- `dotnet restore dotnet/01-Foundation/02-rag/structuredrag_v1_dotnet.cs` succeeded. `dotnet build --no-restore dotnet/01-Foundation/02-rag/structuredrag_v1_dotnet.cs` succeeded and resolved all three local utility projects.
- `dotnet run --no-build --no-restore dotnet/01-Foundation/02-rag/structuredrag_v1_dotnet.cs -- --offline-fixture` succeeded with `SUCCESS: offline ingestion, chunking, retrieval, and context fixture passed.` The fixture uses a temporary local Markdown corpus and verifies Markdown-only ingestion, chunking, lexical retrieval, and deterministic prompt context.
- No normal `dotnet run` was issued. Normal execution invokes a live model and defaults to GitHub ZIP ingestion; neither model/API calls nor GitHub downloads were made.
