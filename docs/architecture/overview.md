# Architecture overview

## System shape

Smartboxx is a **modular monolith with separable workers**. The ASP.NET Core project under `apps/api` is the single backend composition root. Capability modules compile into that host and are not independently deployed applications. The Angular application under `apps/web` is the browser client.

```text
apps/web ──REST──> apps/api (composition root)
                         │
          ┌──────────────┼──────────────┐
          v              v              v
       modules        providers      platform
          │              │              ^
          └──────────────┴──────────────┘
                         contracts

workers ── queues/contracts ──> modular monolith
```

## Boundaries and dependency direction

- `platform` owns stable, vendor-neutral Smartboxx contracts and cross-cutting primitives. It must not contain business workflows or vendor SDK dependencies.
- Each folder under `modules` represents an independently reasoned business capability. Modules may use approved platform contracts but must not reach into another module's internals.
- `apps/api` may compose modules and provider implementations. It contains hosting concerns, not business rules.
- `providers` will implement platform or module-owned interfaces for databases, AI gateways, object storage, vector storage, identity, telemetry, and feature flags. Contracts never depend on providers.
- `workers` are introduced only for processing that needs an independent runtime or scaling profile. Queue/job contracts remain Smartboxx-owned.
- `apps/web` communicates through REST APIs and OpenAPI contracts; it does not couple to server implementation details.
- Client variation belongs in validated configuration and overlays under `clients`, never long-lived client branches.

The intended dependency flow is `host/provider -> application contracts -> domain/platform contracts`. Vendor dependencies remain at the outside edge.

## Capability modules

The initial six capability folders are `documents`, `contact-centre`, `software-delivery`, `testing`, `api-intelligence`, and `value`. Their current assembly markers prove host composition without prematurely designing business functionality. Documents will become the initial Intelligent Document Processing vertical slice in later tasks.

## Deployability

The baseline has two runnable applications: the API and web client. Module assemblies are implementation boundaries inside the API deployment. Workers may become separate deployables where justified. Local packaging will use Docker Compose and deployment assets will remain Helm/Kustomize-ready, but those are intentionally deferred beyond this bootstrap task.
