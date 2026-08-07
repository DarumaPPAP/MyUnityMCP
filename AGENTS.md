# MyUnityMCP Repository Policy

## Repository role

このRepositoryのRelease対象は、Unity Editor向け`unity_graphics_mcp` Package、Tool Schema、Safety Contract、Test、Sample、配布Templateです。UnityAgentMCPとCreator Workflowは設計資産であり、実行可能なControl Planeとして扱いません。

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

## C# rules

- namespaceはFeature単位の単一階層。
- enumは`E_UPPER_SNAKE_CASE`。
- private fieldは`_camelCase`、constは`SCREAMING_SNAKE_CASE`。
- Editor機能はEditor-only Assemblyへ隔離。
- 実装が一つしかない抽象Interfaceを将来予測で追加しない。
- 小規模DTOやEnumを理由なく別Fileへ分割しない。
- Runtimeから`UnityEditor`を参照しない。
- 任意`SerializedProperty`を書き換える汎用Toolを追加しない。

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
