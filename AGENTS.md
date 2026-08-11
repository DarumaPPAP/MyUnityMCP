# MyUnityMCP Repository Policy

## Repository role

このRepositoryのRelease対象は、Unity Editor向け`unity_graphics_mcp` Package、Tool Schema、Safety Contract、Test、Sample、配布Templateです。

実行可能な製品資産と将来設計は物理的に分離します。

- 実行可能製品: `Packages/`、`Catalog/`、`Specs/UnityGraphicsMCP/`、`Tests/`、`SampleProjects/`、`Templates/`
- Design Only: `Design/`

`UnityAgentMCP`、Creator Workflow、Graphics以外の未実装Domain MCPは`Design/`配下の設計資産であり、実行可能なControl PlaneまたはDomainとして扱いません。

対象Unity Project固有のScene、Prefab、Material、Lighting Data、認証情報、組織情報、顧客情報をこのRepositoryへ保存しません。

## Release source of truth

- Version: `VERSION`
- UPM metadata: `Packages/com.darumappap.my-unity-mcp/package.json`
- MCP contract: `Packages/com.darumappap.my-unity-mcp/MCP_MANIFEST.yaml`
- Tool implementation: `Packages/com.darumappap.my-unity-mcp/Editor`
- MCP catalog: `Catalog/mcp-catalog.yaml`
- Capability activation / evidence contract: `Catalog/capability-contracts.yaml`
- Support: `Tests/Compatibility/support-matrix.yaml`
- Current verification: `Tests/Compatibility/release-verification.yaml`
- Historical evidence: `Tests/Compatibility/verification-matrix.yaml`および個別Verification Record

開発順を示す段階名を、現行型名、File名、Tool説明、Error Code、Test名、運用文書へ使用しません。

## Design-only source of truth

- Design module registry: `Design/module-catalog.yaml`
- Creator registry: `Design/Creators/catalog.yaml`
- UnityAgentMCP design: `Design/UnityAgentMCP/spec.md`
- Creator workflows: `Design/Creators/`

Design資産はRelease対象の実装済みCapabilityとして数えません。実装へ昇格する場合は、Package、Operational Catalog、Test、Documentation、Release Contractを同一変更で更新します。

## Tool exposure

全Toolは`AutoRegister = false`を維持し、ClientまたはBridgeの許可リストで必要なToolだけを公開します。

```text
inspect → plan → mutate → save → bake → capture → evaluate → review/refine
```

- Inspect／Plan／PrepareはRead-only。
- MutationはExact Diff、Revision、Approval Token、Baseline再検証が必須。
- SaveとBakeは別承認。
- Automatic Save、Automatic Full Bake、Silent Fallbackは禁止。
- Human ReviewなしにVisual Acceptedとしない。
- Operational Tool Groupは`Catalog/capability-contracts.yaml`に`use_when`、`requires`、`must_not`、`success_evidence`を持つ。
- Tool Group追加時はCatalogだけでなくCapability ContractとRouting Caseも同時更新する。
- `unavailable`は`passed`へ昇格しない。
- Capture成功はVisual Acceptanceではなく、Compile成功はRuntime / target-device Acceptanceではない。
- AgentまたはClientは現在選択されたCapability Contractだけを読み、全Tool Group契約を常時Contextへロードしない。

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
- enumは`E_UPPER_SNAKE_CASE`。
- private fieldは`_camelCase`、constは`SCREAMING_SNAKE_CASE`。
- Editor機能はEditor-only Assemblyへ隔離。
- 実装が一つしかない抽象Interfaceを将来予測で追加しない。
- 小規模DTOやEnumを理由なく別Fileへ分割しない。
- Runtimeから`UnityEditor`を参照しない。
- 任意`SerializedProperty`を書き換える汎用Toolを追加しない。

## Repository layout rules

- `Catalog/`にDesign Only moduleを混在させない。
- ルート`Workflows/`は作成しない。GitHub Actionsは`.github/workflows/`、Creator設計は`Design/Creators/`を使用する。
- `Specs/`は現行の実行可能製品仕様を優先し、将来構想は`Design/`へ隔離する。
- Package内のEditor実装はAssembly境界を維持したまま責務別サブフォルダへ整理可能とし、Release検証は再帰的にToolを検出する。
- Editor実装の標準区分は`Core / Compatibility / Inspection / Planning / Mutation / Save / Bake / Capture / Execution / Tools`とし、Toolごとの過剰なFile分割は行わない。
- 同一内容の仕様をPackage DocumentationとRepository Specsで二重の正本にしない。Package Documentationは利用者向け、Specsは開発・契約向けとする。

## Release rules

- `VERSION`、Package、Manifest、Support Matrix、Changelogを一致させる。
- Release PRではEditor CIとRelease Gateを両方成功させる。
- `Catalog/mcp-catalog.yaml`のOperational Tool Groupと`Catalog/capability-contracts.yaml`のCapabilityを一致させる。
- Known Issuesと未検証範囲を削除・婉曲化しない。
- 一時Migration Script／WorkflowをRelease差分へ残さない。
- 新規ReleaseのTagはRelease Commitへ作成し、公開時点の`main`と同一SHAであることを確認する。
- 公開済みTagは不変とし、移動、削除、Force更新を行わない。
- 公開済みTagの再検証・再配布は、現在の`main`ではなくTagが指すCommitをSourceにする。
- 公開後の`main`更新は許容するが、製品内容を変更する場合はVersionを上げて新しいReleaseを作成する。
- 既存公開Tagの扱い、Release公開、Version更新は人間の明示承認を必須とする。
