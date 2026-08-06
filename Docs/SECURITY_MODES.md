# MyUnityMCP Security Modes

## Purpose

MyUnityMCPは、個人開発、Team開発、制限環境、CIで扱える情報量を分離します。
Security Modeは許可範囲を広げるためではなく、不要な情報を収集・出力しないための境界です。

## Always forbidden

全Modeで次を収集・出力しません。

- Credential
- Authentication Token
- Password
- Unity Project ID
- Organization情報
- Customer名
- 社内Issue番号

これらはRedact対象というだけでなく、Tool Input／Evidence Schema／Catalogの収集項目へ原則追加しません。

## PERSONAL

個人所有Projectで利用します。

許可可能:

- Machine／GPU情報
- Project相対Path
- Object名
- Screenshot／Capture
- Execution Details

常時禁止情報はPERSONALでも除去します。

## TEAM

会社・共同開発Project向けです。

除去:

- Machine／GPU情報
- Project Path
- Object名／Hierarchy Path
- Screenshot／Capture Path
- Execution／Operation情報
- 常時禁止情報

Team Modeでは、結果をCount、Status、Error Code、匿名化された構造情報へ限定します。

## RESTRICTED

既定Modeです。

- Machine情報を除去
- Project Pathを除去
- Screenshotを除去
- Operation情報を除去
- Object名は必要最小限だけ許可
- 常時禁止情報を除去

Mode未指定または未知のMode名はRESTRICTEDになります。

## CI

再現可能な機械判定だけを残します。

許可例:

- Tool名
- Test件数
- Success／Failure
- Structured Error Code
- Compatibility Status
- Artifact Digest

除去:

- Machine情報
- Project Path
- Object名
- Screenshot
- Operation情報
- 常時禁止情報

## Implementation

```text
Packages/com.darumappap.my-unity-mcp/Editor/UnityMcpSecurityPolicy.cs
```

Contract Test:

```text
Packages/com.darumappap.my-unity-mcp/Tests/Editor/UnitySecurityModeTests.cs
```

## Evidence rule

GitHub Actions、External MCP Client E2E、Release Candidate GateのEvidenceはCI Modeで生成します。
Human Visual ReviewでScreenshotが必要な場合も、Team ModeのRepository Evidenceへ自動添付しません。別の明示承認済みReview経路を使用します。

## Prohibited behavior

- Mode未指定をPERSONALとして扱う
- Team ModeでObject名やCustomer Pathを返す
- Screenshotを自動保存・自動Uploadする
- Environment VariableをBuild／Execution Historyへ記録する
- Secretを伏字にして保存する
- Security ModeをSilent Fallbackする
