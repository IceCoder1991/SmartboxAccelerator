# ADR 0002: Kustomize deployment packaging

- **Status:** Accepted
- **Date:** 2026-08-19

## Decision

Use native Kustomize bases and overlays so the same immutable platform images serve every environment and client. Client differences remain configuration, labels, and secret references; Helm templating is intentionally not introduced.
