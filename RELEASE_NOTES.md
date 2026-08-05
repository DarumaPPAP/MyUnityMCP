# MyUnityMCP v1.0.0 Release Notes

MyUnityMCP v1.0.0は、Unity Editor内のGraphics制作を「個別Toolが動く状態」から「長時間運用で停止・診断・再実行できる状態」へ固定する最初の安定Releaseです。

## Highlights

- 32 ToolをInspection、Planning、Mutation、Save、Bake、Capture、Evaluation、Hardeningへ整理
- Exact Diffと一時Approval Tokenによる承認制Mutation
- Dirty Dependency Setによる限定Bake
- COLOR／LINEAR_DEPTH／OBJECT_ID Capture Evidence Bundle
- Human Reviewと自動Evaluationを分離
- Execution History、Progress、Timeout、Cancellation、Lifecycle Recovery
- 新規Unity Project用Getting Started SampleとCI Release Gate

## Compatibility

- Unity Editor `6000.0`以上
- CI検証: `6000.0.75f1`
- Built-in／URP／HDRPはCapability単位で対応状況が異なります。
- Player Runtime、実機上のTool実行は対象外です。

## Upgrade

`0.8.0`からはPackage参照を`v1.0.0`へ固定し、Client設定で必要なToolを明示許可してください。旧Plan、Snapshot、Approval Token、Job ID、Capture IDはSessionを跨いで再利用できません。

## Known Issues

詳細は`Packages/com.darumappap.my-unity-mcp/Documentation~/known-issues.md`を参照してください。
