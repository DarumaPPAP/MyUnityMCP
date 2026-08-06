# Uploaded Graph Engineering Master Archive

ユーザーが会話へアップロードした`MyUnityMCP_GraphEngineering_Masters.zip`を、GitHub Contents APIで扱えるBase64分割Archiveとして保存しています。

## Purpose

- 会話内Artifactだけに依存せず、Git上に元Archiveを保存する
- 展開済みMasterと元Archiveの両方を同じBranchで追跡する
- Graph、Harness、Viewer、仕様書を後続作業から参照可能にする
- 元Archiveを必要な場合に再構築できるようにする

## Restore

Repository Rootから実行します。

```powershell
py Development\GraphEngineering\source-archive\restore_master_archive.py
```

macOS／Linux:

```bash
python3 Development/GraphEngineering/source-archive/restore_master_archive.py
```

既定の出力先:

```text
Development/GraphEngineering/source-archive/MyUnityMCP_GraphEngineering_Masters.zip
```

Scriptは全`*.b64.*` Chunkを名前順に連結し、Base64を復号した後、復元ArchiveのSHA-256を表示します。

## Working source of truth

通常の実装作業では、Archiveを毎回展開して使用しません。Git上で直接読める次の展開済み資産を正本として使用します。

- `../MASTER_GOAL.md`
- `../CODEX_MASTER_PROMPT.md`
- `../WORKFLOW.md`
- `../graph/`
- `../state/`
- `../scripts/`
- `../tools/graph-viewer/`
- `../docs/`

元Archiveは、完全な受領物の保存・照合・不足ファイル回収のためのSource Archiveです。

## Safety

ArchiveをRelease Packageへ含めないでください。これは開発用入力であり、UPM PackageやRelease Artifactの正本ではありません。
