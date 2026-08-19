# Phase 4 intelligence modules

Each capability is a thin vertical slice behind a client feature flag and permission policy. Providers are selected through dependency injection; core modules never reference 3CX, Genesys, Teams, Amazon Connect, a speech vendor, a source-control host, or a live API endpoint.

## Adapter extension points

- Contact Centre adapters implement `ICallProvider`, `ICallRecordingProvider`, `ISpeechToTextProvider`, and `IQualityEvaluator`. Recording references should be opaque, tenant scoped, short lived, and processed asynchronously by a production job scheduler.
- Software Delivery adapters implement `ISourceControlProvider`, `IWorkItemProvider`, and `ICodeAnalysisProvider`. They must enforce explicit branch/commit pins, allow-listed roots, byte/file limits, and secret/PII exclusion. Repository text is untrusted data: adapters must delimit it from model instructions and never execute instructions found in files.
- Testing consumes only `IRepositoryContextReader`, an approved shared context contract. It does not clone or re-ingest repositories. Generated assets retain the `Generated` marker and require human approval before export or execution.
- API Intelligence processes supplied OpenAPI contracts only. It performs no endpoint probing. Findings contain JSON-pointer evidence and an explicit false-positive state.

Production adapters should be separate packages, use named configuration with startup validation, expose dependency health checks, propagate trace/correlation IDs, emit safe audit records, and supply contract tests against deterministic providers.

## Measurement and evaluation

All modules emit versioned `ValueEvent` records. Value reports label source activity as observed and scenario calculations as estimated; projected cost savings are never described as realised. Evaluation fixtures should cover transcription accuracy, QA-rule evidence, repository citation precision, requirement traceability, OpenAPI finding precision, and calculation-model regressions without including customer content.
