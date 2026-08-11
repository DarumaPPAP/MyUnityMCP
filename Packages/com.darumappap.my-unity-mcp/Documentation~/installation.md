# Installation

## Requirements

- Unity `6000.0`以上
- Gitが利用可能な環境
- MCP for Unity Bridge
- Editor Script Compilationが成功しているProject

## 1. Install MCP for Unity

Unity Package Managerの`Add package from git URL`から、検証済みBridge Commitを追加します。

```text
https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#9f84072c38906e3ca903f14f6a8edc1a1c9012c3
```

## 2. Install MyUnityMCP

### Git tag

```text
https://github.com/DarumaPPAP/MyUnityMCP.git?path=/Packages/com.darumappap.my-unity-mcp#v1.0.0
```

Private Repositoryの場合は、Unityを起動するOS UserのGit資格情報が必要です。

### Release `.tgz`

Release Assetの`com.darumappap.my-unity-mcp-1.0.0.tgz`を取得し、Package ManagerからローカルTarballを追加します。

### Embedded package

Repository内の`Packages/com.darumappap.my-unity-mcp`を導入先Projectの`Packages`配下へ配置します。Forkして変更する場合にだけ使用してください。

## 3. Verify

- ConsoleにCompile Errorがない
- `Window > MCP for Unity`が開く
- Bridge Registryが32 Toolを検出する
- Toolが既定では外部公開されていない
- `graphics.inspect_project`が成功する
- Unity 6.7系では`apiCompatibility`に`BASE / UNITY_6000_4 / UNITY_6000_5 / UNITY_6000_7`が出る

## Dependency note

`package.json`はBridge API `10.1.2`を宣言します。Unity `6000.7.0a2`のManual VerificationではMCP for Unity `10.1.3-beta.3`でもPackage Import、Compile、32 Tool Discovery、`graphics.inspect_project`成功を確認しています。Bridgeを独自Versionへ変更した場合はTool DiscoveryとHandler Invocationを再検証してください。
