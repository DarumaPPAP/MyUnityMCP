# MyUnityMCP Repository Policy

## Repository role

このRepositoryの実行可能製品Sourceは、Unity Editor向けMyUnityMCP Tool Surface、そのControl Plane、Tool Schema、Safety Contract、Test、Sample、配布Templateです。

Current `main`にはExact 77 ToolのEditor Surfaceが存在します。各DomainのOperational / Promotion状態は`MCP_MANIFEST.yaml`、`Catalog/`、`Tests/Compatibility/`の現在Evidenceを正本とし、古い45 Tool / Candidate文言だけを根拠に現在状態を判断しません。

実行可能な製品資産と将来設計は物理的・契約的に分離します。

- 実行可能製品: `Packages/`、`Catalog/`、`Specs/`、`Tests/`、`SampleProjects/`、`Templates/`
- Design Only / Future Design: `Design/`

対象Unity Project固有のScene、Prefab、Material、Lighting Data、認証情報、組織情報、顧客情報をこのRepositoryへ保存しません。

## Release source of truth

- Version: `VERSION`
- UPM metadata: `Packages/com.darumappap.my-unity-mcp/package.json`
- MCP contract: `Packages/com.darumappap.my-unity-mcp/MCP_MANIFEST.yaml`
- Tool implementation: `Packages/com.darumappap.my-unity-mcp/Editor`
- MCP catalog: `Catalog/mcp-catalog.yaml`
- Production surface contract: `Catalog/production-surface-contract.yaml`
- Graphics capability contract: `Catalog/capability-contracts.yaml`
- UnityAgent capability contract: `Catalog/unity-agent-capability-contracts.yaml`
- UnityAgent operational spec: `Specs/UnityAgentMCP/spec.md`
- Support: `Tests/Compatibility/support-matrix.yaml`
- Direct Editor verification policy: `Tests/Compatibility/editor-first-verification-policy.yaml`
- Current production Editor evidence: `Tests/Compatibility/production-editor-acceptance.yaml`
- Current production validation evidence: `Tests/Compatibility/production-validation-evidence.yaml`
- Stable release evidence: `Tests/Compatibility/release-verification.yaml`
- Historical release/product state: Git history / immutable release tags

開発順を示す段階名を、現行型名、File名、Tool説明、Error Code、Test名、運用文書へ使用しません。

## Design source of truth

- Design module registry: `Design/module-catalog.yaml`
- Creator registry: `Design/Creators/catalog.yaml`
- Creator workflows: `Design/Creators/`
- Historical design state: Git history / immutable release tags

Design資産はRelease対象の実装済みCapabilityとして数えません。実装へ昇格する場合は、Package、Operational Catalog、Capability Contract、Test、Documentation、Current Spec、Release Contractを同一Delivery変更で更新します。昇格後は`Catalog/`と`Specs/`を現在の実行契約の正本にし、旧Design baselineはcurrent `main`へ二重保持せずGit history / immutable release tagsへ委ねます。

## Tool exposure

全Toolは`AutoRegister = false`を維持し、ClientまたはBridgeの許可リストで必要なToolだけを公開します。

```text
Direct Domain:
inspect → plan → mutate → save → bake → capture → evaluate → review/refine

Agent Control Plane:
inspect capabilities → validate workflow → compile graph → preview
                     → explicit approval → delegate → status/cancel/history
```

- Inspect／Plan／PrepareはRead-only。
- MutationはExact Diff、Revision、Approval Token、Baseline再検証が必須。
- SaveとBakeは別承認。
- Automatic Save、Automatic Full Bake、Silent Fallbackは禁止。
- Human ReviewなしにVisual Acceptedとしない。
- UnityAgentMCPはUnity APIを直接Mutationせず、Operational Catalogに登録されたDomainだけへ委譲する。
- Agentは非Operational Domain、未知Tool Group、依存Cycle、古いEditor Revision、未承認Mutation Groupを拒否する。
- Operational Tool Groupは各Moduleの`capability_contract`に`use_when`、`requires`、`must_not`、`success_evidence`を持つ。
- Tool Group追加時はCatalogだけでなくCapability ContractとRouting／Contract Testも同時更新する。
- `unavailable`は`passed`へ昇格しない。
- Capture成功はVisual Acceptanceではなく、Compile成功はRuntime / target-device Acceptanceではない。
- AgentまたはClientは現在選択されたCapability Contractだけを読み、全Tool Group契約を常時Contextへロードしない。

## Verification authority

MyUnityMCPの通常開発・Promotionでは、Codexが直接アクセス可能な実Unity EditorをPrimary Verification Authorityとします。

```text
Source change
  → Static Contract Check
  → Direct Unity Editor bind/import
  → Unity compile
  → Runtime / Tool discovery
  → Read-only smoke
  → Safety Contract
  → Targeted Editor E2E
  → Regression
  → Cleanup / rollback confirmation
  → Promotion Evidence
  → Human Gate
```

Primary Gateでは、対象変更に応じて次を実観測します。

- UnityによるImport / Compile完了とCompile Error 0
- 契約されたTool / Runtime SurfaceのDiscoveryと重複0
- 対象DomainのRead-only Smoke
- Revision / Approval / one-time Plan / rejection-without-mutation等のSafety Contract
- 承認済みScopeだけを変更するEditor E2E
- Agent Delegation / Failure Propagationなど対象変更に関係する統合挙動
- 既存Production SurfaceのRegression
- Scene / Asset / Revisionのbefore/after Evidence
- Validation Fixture / transient bindingのCleanupまたはRollback

Codexは失敗を観測した場合、最小Root Cause Fix → Unity再Import/Compile → Failed Gate再実行 → Regressionの順で最大3回まで修復Loopを行えます。同一Failureが2回続く、Scope拡張が必要、またはProjectSettings等のHuman Gate対象へ踏み込む場合は停止します。

### GitHub Actions / CI

GitHub ActionsはPrimary GateではなくSupplemental Verificationです。

- CIが利用不能、Runner未開始、`steps=null`の場合は`not_verified`として記録する。
- `not_verified`だけを理由にDirect Unity EditorでPASSしたCandidateのPromotionを禁止しない。
- CIを実行していないのにPASSと記録しない。
- CIが実際にRunner Stepを実行し、Code / Contract起因のFailureを観測した場合は`conflicting_evidence`として扱い、解決またはHuman Reviewまで自動Promotionしない。
- CI PASSは追加Regression Evidenceであり、Direct Unity Editor Evidenceを置き換えない。

### Editor / Target Device separation

Direct Unity Editor Validationを「実機検証」やTarget Device Validationとして記録しません。

- Unity Editor: `direct_editor_validation`
- GitHub Actions: `automated_ci`
- Player / Switch / Android / Console等: `target_device_validation`

Target Device検証は、GoalまたはRelease ContractがPlayer/Device挙動を要求する場合だけ別Gateとして追加します。

## Environment resolution

特定Pipeline、Rendering Path、Target PlatformをPackage全体の固定前提にしません。優先順位は以下です。

1. 対象Projectから検出した事実
2. Requestで明示されたTarget／制約
3. Project固有Profile
4. Client側Preference

`UNVERIFIED`、`UNSUPPORTED`、`BACKEND_NOT_IMPLEMENTED`を区別します。

## Unity API compatibility skill

MyUnityMCPのC#、asmdef、Rendering、Build、UI Toolkit、ECS、XR/AR、Unity Package依存、Unity support versionへ変更を加える場合は、`skills/myunitymcp-unity-api-compatibility/SKILL.md`を必ず適用します。

- Unity API互換性は`BASE`、`UNITY_6000_4`、`UNITY_6000_5`、`UNITY_6000_7`の4 Bucketで保守する。
- Unity 6.6専用Patchを追加せず、6.6由来の変更は`UNITY_6000_7`へRoll-upし、実際の適用VersionはRule lifecycleで保持する。
- 新APIが最低対応Unityでも利用できる場合はVersion Patchへ先送りせずBaseを更新する。
- 正式情報は`CONFIRMED`、Planned breaking changeは`PLANNED`として分離する。
- Package APIはEditor Versionだけで判断せず、対象Package Versionも確認する。
- Compatibility-sensitiveな変更では`Packages/com.darumappap.my-unity-mcp/Editor/Compatibility/ApiCompatibility.cs`と`ApiCompatibilityTests.cs`を同一PRで再評価する。
- 新しいLegacy Unity API呼び出しを追加しない。必要なLegacy対応はCompatibility boundaryへ隔離する。
- 新しいPatch Bucketを追加する場合は、Baseまたは既存Roll-upへ吸収できないことを示し、人間の明示承認を得る。

## C# rules

- namespaceはFeature単位の単一階層。
- `UnityGraphicsMcp` namespace配下の内部型名へ`UnityGraphicsMcp` prefixを重ねない。外部`graphics.*` Tool名は変更しない。
- UnityAgentの外部Tool名は`agent.*`、namespaceは`UnityAgentMcp`を維持する。
- enumは`E_UPPER_SNAKE_CASE`。
- private fieldは`_camelCase`、constは`SCREAMING_SNAKE_CASE`。
- Editor機能はEditor-only Assemblyへ隔離。
- 実装が一つしかない抽象Interfaceを将来予測で追加しない。
- 小規模DTOやEnumを理由なく別Fileへ分割しない。
- Runtimeから`UnityEditor`を参照しない。
- 任意`SerializedProperty`を書き換える汎用Toolを追加しない。

## Repository layout rules

- `Catalog/`にDesign Only moduleを混在させない。
- ルート`Development/`はProduct Repositoryの`main`へ作成しない。開発Control PlaneのGoal、Workflow、Run Record、作業Branch情報は対応する開発Repository側で管理する。
- Graph Engineeringの実行記録・作業履歴をMyUnityMCPのRelease差分へ含めない。MyUnityMCPには昇格後のProduction Contract / Evidenceだけを残す。
- Operational Domain / Control Plane / CreatorのPackage実装は`Packages/com.darumappap.my-unity-mcp/Editor/Operational/`へ配置する。
- Operational Editor Contract Testは`Packages/com.darumappap.my-unity-mcp/Tests/Editor/Operational/`へ配置する。
- ルート`Workflows/`は作成しない。GitHub Actionsは`.github/workflows/`、Creator設計は`Design/Creators/`を使用する。
- `Specs/`は現行の実行可能製品仕様を優先し、将来構想は`Design/`へ隔離する。
- Package内のEditor実装はAssembly境界を維持したまま責務別サブフォルダへ整理可能とし、Release検証は再帰的にToolを検出する。
- Graphics Editor実装の標準区分は`Core / Compatibility / Inspection / Planning / Mutation / Save / Bake / Capture / Execution / Tools`とする。
- 昇格済みControl PlaneはDomain実装と混在させず、専用Feature Folderと専用Contract Testを維持する。
- Toolごとの過剰なFile分割は行わない。
- 同一内容の仕様をPackage DocumentationとRepository Specsで二重の正本にしない。Package Documentationは利用者向け、Specsは開発・契約向けとする。

## Release rules

- `VERSION`、Package、Manifest、Support Matrix、Changelogを一致させる。
- Release / PromotionのPrimary GateはDirect Unity Editor Validationとする。
- GitHub Actions Editor CI / Release GateはSupplemental Evidenceとし、利用不能だけではRelease / PromotionをBlockしない。
- GitHub Actionsが実行済みCode / Contract Failureを返した場合は、Direct Editor PASSとのConflictを解消するかHuman Reviewを通すまでReleaseしない。
- `Catalog/mcp-catalog.yaml`の全Operational Module／Tool Groupと各`capability_contract`を一致させる。
- Known Issuesと未検証範囲を削除・婉曲化しない。
- 一時Migration Script／WorkflowをRelease差分へ残さない。
- 新規ReleaseのTagはRelease Commitへ作成し、公開時点の`main`と同一SHAであることを確認する。
- 公開済みTagは不変とし、移動、削除、Force更新を行わない。
- 公開済みTagの再検証・再配布は、現在の`main`ではなくTagが指すCommitをSourceにする。
- 公開後の`main`更新は許容するが、製品内容を次に公開する場合はVersionを上げて新しいReleaseを作成する。
- 既存公開Tagの扱い、Release公開、Version更新は人間の明示承認を必須とする。
