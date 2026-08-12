#!/usr/bin/env python3
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
CONTRACT_PATH = ROOT / "Development/GraphEngineering/catalog/development-capability-contracts.yaml"
SECURITY_PATH = ROOT / "Development/GraphEngineering/security/security-modes.yaml"
EXPECTED_MODULES = [
    "unity_agent_mcp", "unity_profiler_mcp", "unity_build_mcp", "unity_addressables_mcp",
    "unity_ui_mcp", "unity_animation_mcp", "unity_audio_mcp", "unity_cinematic_mcp",
    "world_creator", "movie_creator", "live_creator"
]
EXPECTED_TOTAL = 59


def main():
    contract = CONTRACT_PATH.read_text(encoding="utf-8")
    security = SECURITY_PATH.read_text(encoding="utf-8")
    errors = []
    total = 0
    for module in EXPECTED_MODULES:
        if not re.search(rf"^  {re.escape(module)}:\s*$", contract, re.MULTILINE):
            errors.append(f"missing contract: {module}")
            continue
        block_match = re.search(rf"^  {re.escape(module)}:\s*\n(?P<body>(?:    .*\n?)*)", contract, re.MULTILINE)
        if not block_match:
            errors.append(f"unreadable contract: {module}")
            continue
        count_match = re.search(r"^    tool_count:\s*(\d+)\s*$", block_match.group("body"), re.MULTILINE)
        if not count_match:
            errors.append(f"missing tool_count: {module}")
            continue
        total += int(count_match.group(1))

    for module in ("unity_agent_mcp", "world_creator", "movie_creator", "live_creator"):
        block = re.search(rf"^  {re.escape(module)}:\s*\n(?P<body>(?:    .*\n?)*)", contract, re.MULTILINE)
        if not block or "direct_unity_mutation: prohibited" not in block.group("body"):
            errors.append(f"direct mutation prohibition missing: {module}")

    if total != EXPECTED_TOTAL:
        errors.append(f"development contract tool total is {total}, expected {EXPECTED_TOTAL}")

    required_security = [
        "default_mode: RESTRICTED", "credentials", "authentication_tokens", "unity_project_id",
        "organization_information", "customer_names", "internal_issue_numbers"
    ]
    for marker in required_security:
        if marker not in security:
            errors.append(f"security marker missing: {marker}")

    result = {
        "module_contracts": len(EXPECTED_MODULES),
        "development_tool_count": total,
        "default_security_mode": "RESTRICTED",
        "errors": errors,
        "status": "pass" if not errors else "failed"
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main())
