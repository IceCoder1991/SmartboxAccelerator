# Smartboxx Client Accelerator

A production-oriented reusable accelerator built as a modular monolith. This baseline contains an ASP.NET Core API host, an Angular application shell, shared platform contracts, and six in-process capability modules. It intentionally contains no business functionality.

## Prerequisites

- [.NET SDK 10.0.100 or a later 10.0 patch](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20.19 or later](https://nodejs.org/) and npm 10 or later
- Git

Confirm the installed tools:

```bash
dotnet --version
node --version
npm --version
```

## Setup from a clean checkout

```bash
git clone <repository-url> SmartboxxAccelerator
cd SmartboxxAccelerator
dotnet restore Smartboxx.slnx
npm install
```

## Build

```bash
dotnet build Smartboxx.slnx --no-restore
npm run build:web
```

## Test

```bash
dotnet test Smartboxx.slnx --no-build
npm run test:web
```

Chrome or Chromium must be installed for the Angular headless browser test.

## Format

```bash
dotnet format Smartboxx.slnx --verify-no-changes
npm run format:check
```

To apply supported formatting changes:

```bash
dotnet format Smartboxx.slnx
npm run format
```

## Run locally

Use two terminals after setup:

```bash
dotnet run --project apps/api/Smartboxx.Api
```

```bash
npm run start --workspace @smartboxx/web -- --open
```

The API is available at the URL printed by ASP.NET Core; check `/health` for readiness. The Angular development server defaults to <http://localhost:4200>.

## Repository layout

| Path                     | Responsibility                                                    |
| ------------------------ | ----------------------------------------------------------------- |
| `apps/api`               | ASP.NET Core composition host; the deployable backend             |
| `apps/web`               | Angular browser application                                       |
| `platform`               | Smartboxx-owned, vendor-neutral shared contracts and primitives   |
| `modules`                | In-process business capabilities; not separate deployables        |
| `providers`              | Future vendor/provider adapters behind Smartboxx contracts        |
| `workers`                | Future separable background or specialist processing boundaries   |
| `clients/template`       | Client overlay template; permanent client branches are prohibited |
| `prompts`, `evaluations` | Versioned AI assets and quality evaluation assets                 |
| `tests`                  | Cross-cutting and host tests                                      |
| `deploy`, `tools`        | Deployment assets and repository automation                       |
| `docs`                   | Architecture decisions and engineering documentation              |

See [the architecture overview](docs/architecture/overview.md) and [ADR 0001](docs/adr/0001-modular-monolith.md).
