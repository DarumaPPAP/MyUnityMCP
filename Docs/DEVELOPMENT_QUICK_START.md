# MyUnityMCP Development Quick Start

## Purpose

`feature/graph-engineering-master`はMyUnityMCPの長期開発環境です。
このBranch全体を`main`へMergeしません。
検証済み製品成果物だけを、最新`main`から作成した`delivery/*`へ移植します。

## 1. Checkout

```powershell
git fetch origin
git switch feature/graph-engineering-master
git pull --ff-only origin feature/graph-engineering-master
```

## 2. Static validation

Repository Rootで実行します。

```powershell
py -m unittest discover -s Development\GraphEngineering\tests -v
py Development\GraphEngineering\scripts\roadmap_harness.py validate
py Development\GraphEngineering\scripts\roadmap_harness.py status
py Development\GraphEngineering\scripts\roadmap_harness.py next
py Development\GraphEngineering\scripts\release_integrity_guard.py --repo-root . --json
py Development\GraphEngineering\scripts\architecture_lint.py --repo-root . --json
```

期待値:

- Python Test: 38件以上PASS
- Candidate Tool: 91
- `AutoRegister = false`: 91
- Manifest Tool Count: 91
- Release Integrity: PASS
- Architecture Lint: PASS

## 3. Graph Dashboard

```powershell
py Development\GraphEngineering\scripts\roadmap_harness.py viewer
```

Browser:

```text
http://127.0.0.1:8765/
```

ViewerはRead-onlyです。Graph／State／Evidenceの代わりにはしません。

## 4. Unity validation

GitHub Actionsの次のWorkflowを手動実行します。

```text
Graph Engineering Unity Validation
```

または、対象BranchをUnity 6000.0.75f1で開いてEditMode Testを実行します。

Gate:

- Compile Error 0
- EditMode Test 158件以上
- Failed 0
- Skipped 0
- Inconclusive 0
- Candidate Tool Discovery 91

GitHub Actionsは結果を次へ保存します。

```text
Development/GraphEngineering/state/evidence/ci/graph-engineering-unity-latest.json
```

## 5. External MCP client E2E

Unity EditorでMCP for UnityのHTTP Transportを起動してから実行します。

```powershell
py Tests\ExternalClient\run_mcp_http_e2e.py `
  --endpoint http://127.0.0.1:8090/mcp `
  --expected-tool graphics.inspect_project `
  --expected-tool agent.inspect_capabilities `
  --output Development\GraphEngineering\state\evidence\external-client\latest.json
```

Port／PathはUnity EditorのMCP Windowに表示されたURLを使用してください。

## 6. Optional backend matrix

Addressablesなし:

- Test Projectの既定構成
- PackageがなくてもCompileする
- Addressables Toolは`UNSUPPORTED`

Addressablesあり:

- `com.unity.addressables`を導入した専用検証Projectを使用
- Settings inspection
- Entry preview
- Approval-gated Entry mutation
- Content Build

Addressables Settings／Groupは自動生成しません。

## 7. Human gates

人間確認が必要です。

- World Visual Review
- Movie Shot Review
- Live Operator Review
- Version承認
- Delivery PR作成承認
- Merge承認
- Release承認

## 8. Release candidate check

全EvidenceとDelivery Manifestを用意した後に実行します。

```powershell
py Development\GraphEngineering\scripts\release_candidate_gate.py `
  --repo-root . `
  --delivery-manifest <Delivery Manifest JSON> `
  --json
```

現時点では未検証GateがあるためFAILするのが正常です。

## 9. Artifact-only delivery

```powershell
git switch main
git pull --ff-only origin main
git switch -c delivery/<capability>
```

承認済み成果物だけを移植し、次を実行します。

```powershell
py Development\GraphEngineering\scripts\delivery_guard.py `
  --base main `
  --head delivery/<capability>
```

ただしDelivery Branchへ`Development/GraphEngineering/**`や`GRAPH_ENGINEERING.md`をコピーしません。
Delivery Guard Scriptは開発Branch側から実行するか、同等のCI Gateとして呼び出します。

## Current truth

- Source implementation: Phase 1〜12 candidate artifacts present
- Static validation: PASS
- Unity Editor CI: pending
- External MCP Client E2E: pending
- Addressables package-present matrix: pending
- Human reviews: pending
- Terminal Goal: false
