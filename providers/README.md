# providers

Provider adapters implement existing Smartboxx contracts and remain separate from module business logic.

The first production adapter is [GitHub source control](github/OPERATIONS.md). It is opt-in and uses a tenant-scoped, least-privilege token obtained through `ISecretProvider`.
