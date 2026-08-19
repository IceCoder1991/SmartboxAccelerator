# Platform foundation

## Configuration and isolation

`Smartboxx` configuration is strongly bound and validated at startup. Client entries contain identity, terminology, feature, model-purpose routing, storage/vector providers, retention, role mapping and safe branding. Environment variables use ASP.NET Core's double-underscore convention (for example `Smartboxx__Clients__acme-demo__Identity__Authority`). Secrets must be supplied by the deployment secret provider and must never be placed in client JSON or returned by bootstrap.

The initial isolation model is **shared infrastructure with a mandatory tenant key**. The request tenant is resolved before authentication/authorisation and unknown tenants are rejected. Every future persistent record, object key, audit event, job and value event must carry this immutable tenant key, and repositories must require it. Database-per-tenant is deliberately not used at this stage. PostgreSQL row-level security is recommended as defence in depth when persistence is introduced.

The bootstrap response is an explicit safe projection: client/product names, terminology, filtered navigation, feature flags, theme tokens, asset references and current-user permissions. Provider endpoints, AI routing and retention configuration are not exposed.

## Identity and OIDC registration

Local header authentication exists only in the `Development` environment. It fails closed elsewhere. Production deployments should select `Oidc`, register one confidential or PKCE public client with the provider, configure an HTTPS redirect URI, logout URI and the API audience, then supply `Authority` and `Audience`. Microsoft Entra ID, Keycloak and standards-compatible client providers are supported by the contract. Provider roles are mapped centrally to Smartboxx permissions; capability modules consume only `ICurrentUser` and `IAuthorisationService`.

## Security and operations baseline

Never log tokens, secrets, raw files, prompts, or unclassified previous/new values. Audit storage is append-oriented and must be writable only by the service identity; production implementations should add hash chaining or immutable archival for tamper awareness. Client confirmation is required for retention, source-IP capture, PII classification, encryption keys, CORS origins, model access and regulatory controls. This design is not a claim of compliance certification.

Trust boundaries are browser → API, API/worker → PostgreSQL, LiteLLM, object storage and external identity/model providers. Controls include TLS, authenticated service identities, tenant-key enforcement, least-privilege permissions, size/type/malware upload hooks, prompt-injection screening, schema validation of AI output, redaction, timeouts/rate limits and trace IDs without sensitive payloads. Metric dimensions must be bounded (service, capability, outcome, model alias); never use user, record or trace IDs as metric labels.

Background jobs remain in the modular-monolith boundary initially. Contracts carry tenant, trace and idempotency context and permit extraction to a worker later. Durable implementations must atomically claim jobs, apply bounded exponential retries/timeouts, support cancellation/progress, and dead-letter exhausted work.
