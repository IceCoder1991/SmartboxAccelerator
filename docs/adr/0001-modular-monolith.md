# ADR 0001: Modular monolith with separable workers

- **Status:** Accepted
- **Date:** 2026-08-19

## Context

Smartboxx needs reusable business capabilities, strong client customisation, and integration with replaceable infrastructure vendors. Creating a service for every capability would add distributed-system cost before independent deployment and scaling needs are demonstrated. Some OCR, vision, audio, ML, and background workloads may nevertheless require different runtimes or scaling profiles.

## Decision

Build the backend as an ASP.NET Core modular monolith. Capability modules are separate assemblies composed by one API host. They communicate through explicit contracts and do not access one another's infrastructure or API implementation. Stable cross-cutting abstractions are Smartboxx-owned and vendor-neutral.

Allow separable workers behind Smartboxx queue/job contracts when workload characteristics justify the boundary. Use Python only for specialist processing where its ecosystem provides a concrete advantage. Keep the Angular web client as a separate frontend application and expose backend functionality through REST with OpenAPI.

Client differences are delivered through configuration and overlays, not permanent branches.

## Consequences

- Local development, transactions, refactoring, and deployment begin simpler than a distributed service architecture.
- Assembly and architecture tests must enforce boundaries because process isolation does not.
- Providers and workers can be replaced or extracted without changing business contracts.
- Module extraction remains possible, but is a deliberate future decision rather than the default.
- A single API deployment can scale only as a unit until a justified worker or service boundary is extracted.
