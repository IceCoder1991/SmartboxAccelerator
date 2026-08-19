# Deployment

`base` packages the web, API, worker, migration Job, and LiteLLM. Environment and generated client overlays compose it using Kustomize. Images are immutable in a release (production automation should pin digests), run non-root with dropped capabilities, probes and resource bounds. Create `smartboxx-runtime` in the target secret manager; no Secret manifest belongs in Git.

Run the migration Job before rolling deployments. Migrations follow expand/migrate/contract: releases remain compatible with the previous minor database shape, and destructive contraction occurs only after the old release is retired. Roll back workloads to the prior image/config, but do not reverse a shared database migration; apply a tested forward repair. Back up external object/database storage before upgrades. Persistent databases, object storage, backup, encryption, retention, and regional topology are operator-managed; local clusters may add disposable PVCs only.

NetworkPolicy requires a supporting CNI. Narrow the supplied starter egress/ingress rules for each cloud. Render with `kubectl kustomize deploy/overlays/<environment>` and validate with `tools/validate-manifests.py`.
