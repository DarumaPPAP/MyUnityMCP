#!/usr/bin/env python3
from pathlib import Path
import argparse, json, re, sys

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--mode', choices=['pr','tag'], default='pr')
parser.add_argument('--tag', default='')
args = parser.parse_args()

version = (ROOT/'VERSION').read_text(encoding='utf-8').strip()
required = [
    'README.md','CHANGELOG.md','RELEASE_NOTES.md','VERSION',
    'Packages/com.darumappap.my-unity-mcp/package.json',
    'Packages/com.darumappap.my-unity-mcp/MCP_MANIFEST.yaml',
    'Packages/com.darumappap.my-unity-mcp/README.md',
    'Packages/com.darumappap.my-unity-mcp/CHANGELOG.md',
    'Packages/com.darumappap.my-unity-mcp/LICENSE.md',
    'Packages/com.darumappap.my-unity-mcp/Documentation~/quick-start.md',
    'Packages/com.darumappap.my-unity-mcp/Documentation~/installation.md',
    'Packages/com.darumappap.my-unity-mcp/Documentation~/mcp-client-configuration.md',
    'Packages/com.darumappap.my-unity-mcp/Documentation~/tool-reference.md',
    'Packages/com.darumappap.my-unity-mcp/Documentation~/status-and-error-codes.md',
    'Packages/com.darumappap.my-unity-mcp/Documentation~/safety-model.md',
    'Packages/com.darumappap.my-unity-mcp/Documentation~/bake-constraints.md',
    'Packages/com.darumappap.my-unity-mcp/Documentation~/pipeline-support.md',
    'Packages/com.darumappap.my-unity-mcp/Documentation~/troubleshooting.md',
    'Packages/com.darumappap.my-unity-mcp/Documentation~/sample-workflow.md',
    'Packages/com.darumappap.my-unity-mcp/Documentation~/upgrade-guide.md',
    'Packages/com.darumappap.my-unity-mcp/Documentation~/known-issues.md',
    'Specs/UnityWorldCreatorMCP/spec.md',
    'Catalog/world-creator-capability-contract.yaml',
    'Templates/McpClients/generic-http.json.example',
    'Templates/McpClients/codex.toml.example',
    'Templates/AcceptanceProfiles/balanced-graphics.json',
    'Templates/CI/myunitymcp-unity-ci.yml',
    'Tests/Compatibility/support-matrix.yaml',
    'Tests/Compatibility/release-verification.yaml',
]
missing=[p for p in required if not (ROOT/p).is_file()]
if missing: raise SystemExit('Missing release files:\n'+'\n'.join(missing))

package=json.loads((ROOT/'Packages/com.darumappap.my-unity-mcp/package.json').read_text(encoding='utf-8'))
if package['version'] != version: raise SystemExit('VERSION and package.json differ')
if package.get('samples'):
    raise SystemExit('Production package must not publish Samples')
manifest=(ROOT/'Packages/com.darumappap.my-unity-mcp/MCP_MANIFEST.yaml').read_text(encoding='utf-8')
support=(ROOT/'Tests/Compatibility/support-matrix.yaml').read_text(encoding='utf-8')
changelog=(ROOT/'CHANGELOG.md').read_text(encoding='utf-8')
verification=(ROOT/'Tests/Compatibility/release-verification.yaml').read_text(encoding='utf-8')
for name,text,pattern in [
    ('manifest',manifest,rf'^version: "{re.escape(version)}"$'),
    ('support matrix',support,rf'^package_version: "{re.escape(version)}"$'),
    ('changelog',changelog,rf'^## \[{re.escape(version)}\] - '),
]:
    if not re.search(pattern,text,re.MULTILINE): raise SystemExit(f'{name} version mismatch')

manifest_tool_count_match = re.search(r'^\s*discovered_tool_count:\s*(\d+)\s*$', manifest, re.MULTILINE)
verification_tool_count_match = re.search(r'^\s*tool_discovery_count:\s*(\d+)\s*$', manifest, re.MULTILINE)
if not manifest_tool_count_match or not verification_tool_count_match:
    raise SystemExit('Manifest must declare bridge.discovered_tool_count and verification.tool_discovery_count')
expected_tool_count = int(manifest_tool_count_match.group(1))
if int(verification_tool_count_match.group(1)) != expected_tool_count:
    raise SystemExit('Manifest discovery counts disagree')

for path in [
    'Templates/McpClients/generic-http.json.example',
    'Templates/McpClients/recommended-readonly-allowlist.json',
    'Templates/AcceptanceProfiles/balanced-graphics.json',
]:
    json.loads((ROOT/path).read_text(encoding='utf-8'))

editor_root=ROOT/'Packages/com.darumappap.my-unity-mcp/Editor'
source='\n'.join(p.read_text(encoding='utf-8') for p in editor_root.rglob('*.cs'))
count=len(re.findall(r'\[McpForUnityTool\s*\(',source))
disabled=len(re.findall(r'AutoRegister\s*=\s*false',source))
if count != expected_tool_count:
    raise SystemExit(f'Expected {expected_tool_count} MCP tools from manifest, found {count}')
if disabled != count:
    raise SystemExit(f'Expected every tool to be disabled by default, found {disabled}/{count}')

active_paths=[ROOT/'README.md',ROOT/'AGENTS.md',ROOT/'Catalog',ROOT/'Design',ROOT/'Packages/com.darumappap.my-unity-mcp/Editor',ROOT/'Packages/com.darumappap.my-unity-mcp/Tests/Editor']
for base in active_paths:
    files=[base] if base.is_file() else [p for p in base.rglob('*') if p.is_file() and p.suffix in {'.md','.yaml','.cs'}]
    for path in files:
        text=path.read_text(encoding='utf-8')
        if re.search(r'Phase\s*\d|Phase\d|phase_\d',text,re.IGNORECASE):
            raise SystemExit(f'Delivery phase wording remains in active file: {path.relative_to(ROOT)}')

for obsolete in [
    '.github/workflows/release-source-audit-export.yml',
    '.github/workflows/apply-v1-release.yml',
    'Tools/apply-v1-release.py',
    'Specs/UnityGraphicsMCP/plan.md',
    'Specs/UnityGraphicsMCP/tasks.md',
    'Specs/UnityGraphicsMCP/editor-tool-design.md',
    'Packages/com.darumappap.my-unity-mcp/Samples~',
    'SampleProjects',
]:
    if (ROOT/obsolete).exists(): raise SystemExit(f'Obsolete or temporary file remains: {obsolete}')

if args.mode == 'tag':
    if args.tag != 'v'+version: raise SystemExit('Tag and VERSION differ')
    verification_version = re.search(r'^release_version:\s*"?([^"\s]+)"?\s*$', verification, re.MULTILINE)
    if not verification_version or verification_version.group(1) != version:
        raise SystemExit('Release evidence version does not match VERSION')
    if 'verification_status: passed' not in verification:
        raise SystemExit('Release evidence is not passed')

print(f'Release contract PASS: version={version}, tools={count}, required_files={len(required)}')
