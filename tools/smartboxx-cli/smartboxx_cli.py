"""Dependency-free Smartboxx client overlay generator and validator."""
from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
from pathlib import Path

CLI_VERSION = "0.1.0"
ROOT = Path(__file__).resolve().parents[2]
CLIENT_RE = re.compile(r"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$")
CHOICES = {
    "cloud": ("azure", "aws", "gcp", "local"),
    "identity": ("entra-id", "okta", "auth0", "keycloak", "synthetic"),
    "storage": ("azure-blob", "s3", "gcs", "filesystem"),
    "vector_store": ("postgresql", "azure-ai-search", "opensearch", "none"),
}
CAPABILITIES = ("documents", "value-dashboard", "human-review")
ENVIRONMENTS = ("local", "demo", "client-test", "client-production")


def csv(value: str) -> list[str]:
    return [item.strip() for item in value.split(",") if item.strip()]


def validate_id(value: str) -> str:
    if not CLIENT_RE.fullmatch(value) or value in {"template", ".", ".."}:
        raise argparse.ArgumentTypeError("use lowercase letters, digits and single hyphens (for example client-abc)")
    return value


def dump(value: object) -> str:
    return json.dumps(value, indent=2, sort_keys=True) + "\n"


def config(args: argparse.Namespace) -> dict:
    providers = csv(args.ai_providers)
    capabilities = csv(args.capabilities)
    environments = csv(args.environments)
    unknown_caps = sorted(set(capabilities) - set(CAPABILITIES))
    unknown_envs = sorted(set(environments) - set(ENVIRONMENTS))
    if not providers or unknown_caps or unknown_envs:
        raise ValueError(f"invalid selection: providers must be non-empty; capabilities={unknown_caps}; environments={unknown_envs}")
    return {
        "schemaVersion": 1,
        "platformVersion": platform_version(),
        "client": {"id": args.client_id, "name": args.name, "industry": args.industry},
        "cloud": args.cloud,
        "identity": {"provider": args.identity, "settingsFromEnvironment": True},
        "ai": {"allowedProviders": providers, "liteLLM": {"modelListConfig": "litellm/models.yaml", "settingsFromEnvironment": True}},
        "storage": {"provider": args.storage, "settingsFromEnvironment": True},
        "vectorStore": {"provider": args.vector_store, "settingsFromEnvironment": True},
        "capabilities": {name: name in capabilities for name in CAPABILITIES},
        "environments": environments,
    }


def platform_version() -> str:
    return json.loads((ROOT / "release.json").read_text())["version"]


def write(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content)


def overlay_kustomization(client_id: str, environment: str, version: str) -> str:
    return f"""apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
namespace: smartboxx-{client_id}-{environment}
resources:
  - ../../../../deploy/base
labels:
  - pairs:
      app.kubernetes.io/instance: {client_id}
      smartboxx.io/platform-version: {version}
configMapGenerator:
  - name: smartboxx-client
    files:
      - ../../../client.json
    literals:
      - SMARTBOXX_ENVIRONMENT={environment}
"""


def generate(args: argparse.Namespace) -> Path:
    target = ROOT / "clients" / args.client_id
    if target.exists():
        if not args.force:
            raise FileExistsError(f"{target} exists; pass --force to replace it")
        shutil.rmtree(target)
    cfg = config(args)
    write(target / "client.json", dump(cfg))
    write(target / "branding/theme.json", dump({"productName": args.name, "primaryColor": "#006F70", "logo": "logo.svg"}))
    write(target / "branding/logo.svg", '<svg xmlns="http://www.w3.org/2000/svg" width="240" height="64"><rect width="240" height="64" fill="#006F70"/><text x="16" y="40" fill="white">Replace logo</text></svg>\n')
    for folder in ("prompts", "document-definitions", "integrations"):
        write(target / folder / "README.md", f"# {folder.replace('-', ' ').title()}\n\nVersion-controlled client {folder}; do not store secrets here.\n")
    write(target / "integrations/provider.stub.json", dump({"enabled": False, "configurationFromEnvironment": True}))
    write(target / "litellm/models.yaml", "# Add model aliases only; API keys are supplied through secret references.\nmodel_list: []\n")
    write(target / "feature-flags.json", dump(cfg["capabilities"]))
    for env in cfg["environments"]:
        write(target / f"deploy/{env}/kustomization.yaml", overlay_kustomization(args.client_id, env, cfg["platformVersion"]))
    write(target / ".env.example", "# Names only: never commit values or secrets.\nSMARTBOXX_IDENTITY_AUTHORITY=\nSMARTBOXX_STORAGE_ENDPOINT=\nLITELLM_MASTER_KEY=\n")
    write(target / "README.md", f"# {args.name}\n\nPlatform `{cfg['platformVersion']}` overlay for `{args.client_id}`.\n\nValidate with `tools/smartboxx-cli/smartboxx validate-client clients/{args.client_id}`. Secrets must be injected by the deployment platform.\n")
    return target


def validate(path: Path) -> list[str]:
    errors: list[str] = []
    try:
        resolved = path.resolve()
        clients = (ROOT / "clients").resolve()
        if clients not in resolved.parents or not CLIENT_RE.fullmatch(resolved.name):
            return ["client directory must be a safely named direct child of clients/"]
        cfg = json.loads((resolved / "client.json").read_text())
    except (OSError, json.JSONDecodeError) as exc:
        return [f"cannot read client.json: {exc}"]
    if cfg.get("client", {}).get("id") != resolved.name:
        errors.append("client.id must match directory name")
    if cfg.get("schemaVersion") != 1:
        errors.append("unsupported schemaVersion")
    version = cfg.get("platformVersion", "")
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", version):
        errors.append("platformVersion must be semantic")
    required = ["branding/theme.json", "feature-flags.json", ".env.example", "prompts", "document-definitions", "integrations"]
    for item in required:
        if not (resolved / item).exists(): errors.append(f"missing {item}")
    env_text = (resolved / ".env.example").read_text() if (resolved / ".env.example").exists() else ""
    for line in env_text.splitlines():
        if line and not line.startswith("#") and "=" in line and line.split("=", 1)[1]: errors.append(".env.example must not contain values")
    return errors


def seed_demo(path: Path, reset: bool) -> None:
    errors = validate(path)
    if errors: raise ValueError("; ".join(errors))
    cfg = json.loads((path / "client.json").read_text())
    if "client-production" in cfg["environments"]:
        # Production may exist, but demo activation is restricted to a separate demo overlay.
        pass
    demo = path / "demo"
    if demo.exists():
        if not reset: raise FileExistsError("demo already exists; use reset-demo for deterministic replacement")
        shutil.rmtree(demo)
    fixture = {"demo": True, "synthetic": True, "label": "DEMO — SYNTHETIC DATA", "environment": "demo", "productionAllowed": False,
               "heroWorkflow": "invoice-processing", "expectedResults": {"invoiceNumber": "SYN-1001", "total": 1250.0},
               "valueAssumptions": {"minutesSavedPerDocument": 12, "hourlyCost": 45}}
    write(demo / "demo.json", dump(fixture))
    write(demo / "document-definitions/invoice.schema.json", dump({"type": "object", "required": ["invoiceNumber", "total"], "properties": {"invoiceNumber": {"type": "string"}, "total": {"type": "number"}}}))
    write(demo / "prompts/extract-invoice.md", "DEMO / SYNTHETIC: Extract invoiceNumber and total. Never represent these results as production data.\n")
    write(demo / "rules/invoice.json", dump({"total": {"minimum": 0}, "requiresHumanReview": True}))
    write(demo / "sample-files/invoice-SYN-1001.txt", "DEMO SYNTHETIC INVOICE\nInvoice: SYN-1001\nTotal: 1250.00\n")
    write(demo / "state.json", dump({"uploads": [{"file": "invoice-SYN-1001.txt", "progress": 100, "status": "review", "synthetic": True}], "reviewItems": 1, "dashboard": {"documents": 1, "hoursSaved": 0.2, "estimatedValue": 9.0}}))


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(prog="smartboxx")
    p.add_argument("--version", action="version", version=CLI_VERSION)
    sub = p.add_subparsers(dest="command", required=True)
    n = sub.add_parser("new-client")
    n.add_argument("client_id", type=validate_id); n.add_argument("--name", required=True); n.add_argument("--industry", required=True)
    for key, choices in CHOICES.items(): n.add_argument("--" + key.replace("_", "-"), dest=key, choices=choices, required=True)
    n.add_argument("--ai-providers", required=True); n.add_argument("--capabilities", default=",".join(CAPABILITIES)); n.add_argument("--environments", default=",".join(ENVIRONMENTS)); n.add_argument("--force", action="store_true")
    for command in ("validate-client", "seed-demo", "reset-demo", "check-compatibility"):
        c = sub.add_parser(command); c.add_argument("path", type=Path)
    return p


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    try:
        if args.command == "new-client": print(generate(args)); return 0
        if args.command in {"seed-demo", "reset-demo"}: seed_demo(args.path, args.command == "reset-demo"); print(args.path / "demo"); return 0
        errors = validate(args.path)
        if args.command == "check-compatibility" and not errors:
            cfg = json.loads((args.path / "client.json").read_text())
            current = platform_version().split("."); client = cfg["platformVersion"].split(".")
            if client[0] != current[0]: errors.append(f"major version {client[0]} is incompatible with platform {platform_version()}")
            for folder in ("prompts", "document-definitions", "integrations", "deploy"):
                if not (args.path / folder).exists(): errors.append(f"upgrade review required: missing {folder}")
        if errors:
            for error in errors: print(f"ERROR: {error}", file=sys.stderr)
            return 1
        print(f"valid: {args.path}"); return 0
    except (ValueError, FileExistsError, OSError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr); return 2


if __name__ == "__main__": raise SystemExit(main())
