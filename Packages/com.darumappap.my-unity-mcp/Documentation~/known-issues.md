# Known Issues

## External MCP Client connection

CIはBridge RegistryとDirect Handler Invocationを検証しますが、すべての外部MCP Clientとの実接続は検証していません。Client Version更新後は接続とAllowlistを再確認してください。

## Client disconnect notification

切断をExecutionへ反映するには、Transport Adapterが`NotifyClientDisconnected`を呼ぶ必要があります。未対応ClientではTimeoutまたはUnity Lifecycle Eventで中断が確定します。

## Capture in BatchMode

NoGraphicsのCIでは実画像生成を検証しません。Capture State Machine、Manifest、Digest、Cleanup ContractはTestしますが、実画像はGraphics Deviceを持つEditorで確認してください。

## APV

Built-in Pipelineは非対応です。URP／HDRPの実APV BakeはProject固有Baking Set、Lighting Scenario、Package Version、Output Rootへ依存し、CIでは実Bakeを偽装しません。

## Dependency Bake

Reflection Probeは既存Cubemap Assetが必要です。Lightmap／APVを含むBake出力はUnity Undoや自動Rollbackで元に戻りません。

## Coverage

OBJECT_ID CaptureはRenderer単位です。Terrain、Decal Projector、Procedural Draw、Material固有Alpha Clip、頂点変形等はManifestのCoverage Limitationsへ記録されます。

## Platforms

Player Runtime、PC／Console／Mobile／Nintendo Switch等のTarget Device上でToolを実行する機能はありません。
