# GitHub source-control provider operations

## Scope and security

This adapter implements only `ISourceControlProvider`; it does not change software-delivery business logic. Each tenant has a separate allow-list and secret reference. Use a fine-grained GitHub token (or GitHub App installation token supplied by the secret implementation) with **Contents: read-only** access to only the allow-listed repositories. Do not grant administration, issues, pull requests, workflows, or write permissions.

Configure `GitHubProviderOptions.Tenants[tenantId]` with `TokenSecretName` and `AllowedRepositories`. The referenced value must be resolved by the deployment `ISecretProvider`; never place a token in JSON, environment-backed client configuration, logs, or traces. Rotate the secret in the external store and restart/refresh its provider according to that store's procedure.

## Behaviour

* Reads require a pinned hexadecimal commit and are therefore idempotent. Branch is descriptive and is never used to fetch mutable content.
* GitHub's recursive tree response is streamed. A truncated tree is rejected rather than silently returning incomplete data; operators must reduce repository scope. Blob downloads stop at configured file and byte limits.
* Paths and content likely to contain credentials are excluded. Tokens and tenant identifiers are never emitted; telemetry uses a short tenant hash.
* `429`, transient `5xx`, timeouts, and primary rate-limit responses are retried with bounded exponential or server-directed delay. The adapter never retries authorization or ordinary forbidden responses.
* Tenant configuration and repository allow-list checks occur before any network call. A provider instance must receive the request-bound `ITenantContextAccessor`.

## Health and telemetry

Call `CheckHealthAsync` from the host's provider-specific health registration. It requests GitHub's rate-limit endpoint using the tenant credential and reports only success/failure. Do not expose upstream response bodies in public health output.

The adapter emits `Smartboxx.Providers.GitHub` activities and the `smartboxx.github.requests` counter. Alert on sustained retry volume, health failure, rate-limit exhaustion, tree truncation, and ingestion limit failures. Logs around this adapter must redact `Authorization`, secret names, response bodies, repository file contents, and raw tenant IDs.

## Verification and incident response

Unit/contract tests use a fake HTTP handler and no external credentials. The live test runs only when all of `SMARTBOXX_GITHUB_INTEGRATION=1`, `SMARTBOXX_GITHUB_TOKEN`, `SMARTBOXX_GITHUB_REPOSITORY`, and `SMARTBOXX_GITHUB_COMMIT` are explicitly supplied. Use a dedicated read-only test repository and ephemeral credential.

If a token may have leaked: revoke it in GitHub immediately, rotate the external secret, inspect GitHub audit logs, stop affected ingestion, and follow the client incident process. If tenant routing is in doubt, disable the tenant's provider configuration rather than widening its allow-list.
