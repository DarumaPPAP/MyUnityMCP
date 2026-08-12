#!/usr/bin/env python3
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
CONTRACT_PATH = ROOT / "Development/GraphEngineering/catalog/development-capability-contracts.yaml"
SECURITY_PATH = ROOT / "Development/GraphEngineering/security/security-modes.yaml"
PRODUCTION_MANIFEST = ROOT / "Packages/com.darumappap.my-unity-mcp/MCP_MANIFEST.yaml"
EXPECTED_MODULES = [
    "unity_profiler_mcp", "unity_build_mcp", "unity_addressables_mcp",
    "unity_ui_mcp", "unity_animation_mcp", "unity_audio_mcp", "unity_cinematic_mcp",
    "world_creator", "movie_creator", "live_creator"
]
EXPECTED_TOTAL = 49


def main():
    contract = CONTRACT_PATH.read_text(encoding="utf-8")
    security = SECURITY_PATH.read_text(encoding="utf-8")
    production_manifest = PRODUCTION_MANIFEST.read_text(encoding="utf-8")
    errors = []
    total = 0

    contracts_section = contract.split("\ncontracts:\n", 1)[1] if "\ncontracts:\n" in contract else ""
    for module in EXPECTED_MODULES:
        if not re.search(rf"^  {re.escape(module)}:\s*$", contracts_section, re.MULTILINE):
            errors.append(f"missing contract: {module}")
            continue
        block_match = re.search(rf"^  {re.escape(module)}:\s*\n(?P<body>(?:    .*\n?)*)", contracts_section, re.MULTILINE)
        if not block_match:
            errors.append(f"unreadable contract: {module}")
            continue
        count_match = re.search(r"^    tool_count:\s*(\d+)\s*$", block_match.group("body"), re.MULTILINE)
        if not count_match:
            errors.append(f"missing tool_count: {module}")
            continue
        total += int(count_match.group(1))

    for module in ("world_creator", "movie_creator", "live_creator"):
        block = re.search(rf"^  {re.escape(module)}:\s*\n(?P<body>(?:    .*\n?)*)", contracts_section, re.MULTILINE)
        if not block or "direct_unity_mutation: prohibited" not in block.group("body"):
            errors.append(f"direct mutation prohibition missing: {module}")

    if total != EXPECTED_TOTAL:
        errors.append(f"development contract tool total is {total}, expected {EXPECTED_TOTAL}")

    if "unity_agent_mcp:" not in production_manifest or "agent_tool_count: 10" not in production_manifest:
        # MCP_MANIFEST uses the agent tool group rather than an explicit agent_tool_count field.
        agent_tools = len(re.findall(r"^\s+- agent\.", production_manifest, re.MULTILINE))
        if agent_tools != 10:
            errors.append(f"promoted Agent production contract is missing or has {agent_tools} tools, expected 10")

    required_security = [
        "default_mode: RESTRICTED", "credentials", "authentication_tokens", "unity_project_id",
        "organization_information", "customer_names", "internal_issue_numbers"
    ]
    for marker in required_security:
        if marker not in security:
            errors.append(f"security marker missing: {marker}")

    result = {
        "remaining_module_contracts": len(EXPECTED_MODULES),
        "remaining_development_tool_count": total,
        "promoted_agent_tool_count": 10,
        "final_combined_target": 91,
        "default_security_mode": "RESTRICTED",
        "errors": errors,
        "status": "pass" if not errors else "failed"
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main())
