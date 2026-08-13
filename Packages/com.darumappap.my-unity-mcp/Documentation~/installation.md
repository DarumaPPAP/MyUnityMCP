# Installation

## Requirements

- Unity `6000.0`以上
- Gitが利用可能な環境
- MCP for Unity Bridge
- Editor Script Compilationが成功しているProject

## 1. Install MCP for Unity

Unity Package Managerの`Add package from git URL`から、MCP for Unity Bridgeを追加します。MyUnityMCP `package.json`はBridge API `10.1.2`を依存Versionとして宣言します。

## 2. Install MyUnityMCP

### Git tag

```text
https://github.com/DarumaPPAP/MyUnityMCP.git?path=/Packages/com.darumappap.my-unity-mcp#v1.1.0
```

Private Repositoryの場合は、Unityを起動するOS UserのGit資格情報が必要です。

### Release `.tgz`

Release Assetの`com.darumappap.my-unity-mcp-1.1.0.tgz`を取得し、Package ManagerからローカルTarballを追加します。

### Embedded package

Repository内の`Packages/com.darumappap.my-unity-mcp`を導入先Projectの`Packages`配下へ配置します。Forkして変更する場合にだけ使用してください。

## 3. Verify

- ConsoleにCompile Errorがない
- `Window > MCP for Unity`が開く
- Bridge RegistryがMyUnityMCP **77 Tool**を検出する
- Duplicate Toolがない
- Toolが既定では外部公開されていない（`AutoRegister = false`）
- `graphics.inspect_project`が成功する
- `agent.inspect_capabilities`でOperational Domainを確認できる
- Unity 6.7系では`apiCompatibility`に`BASE / UNITY_6000_4 / UNITY_6000_5 / UNITY_6000_7`が出る

## Optional Addressables

`com.unity.addressables`が未導入の場合、Addressables Domainは自動導入せず`UNSUPPORTED`を返します。Settings / Group自動生成やContent BuildへのFallbackは行いません。

## Dependency note

Unity `6000.7.0a2` Direct Editor VerificationではPackage Compile、Exact 77 Tool Discovery、`graphics.inspect_project`、Agent Routing、Safety / Scoped Mutation E2Eを確認しています。Bridgeを独自Versionへ変更した場合は77 Tool DiscoveryとHandler Invocationを再確認してください。
