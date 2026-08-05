# Upgrade Guide

## From 0.8.x to 1.0.0

1. 作業中のExecutionを完了またはCancelします。
2. Unityを閉じ、ProjectをVersion ControlへCommitします。
3. Package参照を`v1.0.0`へ固定します。
4. Unityを開き、Compileと32 Tool Discoveryを確認します。
5. ClientのAllowlistを見直します。
6. 古いSnapshot、Plan、Approval Token、Job ID、Capture IDを破棄します。
7. `graphics.get_support_matrix`を取得し、Pipeline条件を再確認します。

## Renamed diagnostics

開発段階名を含んでいたEnvironment MutationのIssue Codeは`GFX-ENVIRONMENT-*`へ変更されました。旧Code名に依存するLog Parserを更新してください。

## Behavioral compatibility

安全境界は維持されています。MutationへSave／Bakeが自動追加されることはありません。Built-in PipelineのAPVはPlan準備時に拒否されます。
