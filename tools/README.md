# Tools

## Smartboxx client CLI

The dependency-free CLI supports CI-friendly generation, validation, compatibility checks, and deterministic demo fixtures. Every generation input is explicit; no secret is accepted or generated.

```bash
tools/smartboxx-cli/smartboxx new-client client-abc --name "Client ABC" --industry insurance --cloud azure --identity entra-id --ai-providers azure-openai --storage azure-blob --vector-store postgresql
tools/smartboxx-cli/smartboxx validate-client clients/client-abc
tools/smartboxx-cli/smartboxx check-compatibility clients/client-abc
```

Safe slug validation prevents traversal. Existing directories are rejected unless `--force` is explicitly supplied. Use `.env.example` only as a list of variables and inject values from a secret manager.
