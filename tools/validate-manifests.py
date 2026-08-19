#!/usr/bin/env python3
from pathlib import Path
import sys
root = Path(__file__).resolve().parents[1]
errors=[]
for path in sorted((root/'deploy').rglob('*.yaml')):
    text=path.read_text()
    if 'kind:' not in text or 'apiVersion:' not in text: errors.append(f'{path}: missing Kubernetes fields')
base=(root/'deploy/base/workloads.yaml').read_text()
for requirement in ('runAsNonRoot: true','allowPrivilegeEscalation: false','resources:','readinessProbe:','secretRef:'):
    if requirement not in base: errors.append(f'base missing {requirement}')
production='\n'.join(p.read_text() for p in (root/'deploy/overlays/client-production').rglob('*.yaml'))
if 'synthetic' in production.lower() or 'demo' in production.lower(): errors.append('production overlay must not activate demo/synthetic mode')
print('\n'.join(errors) if errors else 'deployment manifests passed static policy checks')
sys.exit(bool(errors))
