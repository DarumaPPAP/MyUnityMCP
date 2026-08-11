# Upgrade Guide

## Canonical v1.0.0

`v1.0.1`および`v1.0.2-test.*`を使用していた場合は、Package参照をCanonical `v1.0.0`へ固定し直してください。

1. Package参照を`v1.0.0`へ更新します。
2. Unityを開き、Compileと32 Tool Discoveryを確認します。
3. `graphics.inspect_project`を実行し、Unity VersionとCompatibility Bucketを確認します。
4. 既存のClient Allowlist、Acceptance Profile、Project設定はそのまま利用できます。
5. Session内の古いSnapshot、Plan、Approval Token、Job ID、Capture IDは再利用せず破棄してください。

今回のCanonical v1.0.0では、外部MCP Tool名`graphics.*`とSafety Boundaryを維持したまま、Unity 6.4 / 6.5 / 6.7互換対応と内部C# Naming / Folder構成を整理しています。

## From 0.8.x to 1.0.0

1. 作業中のExecutionを完了またはCancelします。
2. Unityを閉じ、ProjectをVersion ControlへCommitします。
3. Package参照を`v1.0.0`へ固定します。
4. Unityを開き、Compileと32 Tool Discoveryを確認します。
5. ClientのAllowlistを見直します。
6. 古いSnapshot、Plan、Approval Token、Job ID、Capture IDを破棄します。
7. `graphics.get_support_matrix`を取得し、Pipeline条件を再確認します。

## Internal refactor impact

Package内部では`UnityGraphicsMcpFoo`型を`Foo`へ短縮し、Editorコードを責務別Folderへ移動しています。これらは内部実装であり、外部MCP Tool名`graphics.*`は変更していません。Package内部型へ直接依存していた独自Editor拡張だけは再コンパイルと参照更新が必要です。

## Behavioral compatibility

安全境界は維持されています。MutationへSave／Bakeが自動追加されることはありません。Built-in PipelineのAPVはPlan準備時に拒否されます。
