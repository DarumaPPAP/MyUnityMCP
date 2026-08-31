# Compatibility Evidence

## Current v1.1.0 Sources of Truth

- `production-editor-acceptance.yaml`: Unity `6000.7.0a2` Direct EditorでのExact 77 Tool Acceptance Evidence
- `production-validation-evidence.yaml`: 現行77 ToolのValidation結果と残存`not_verified`範囲
- `editor-first-verification-policy.yaml`: Direct Unity Editor Primary / CI SupplementalのVerification Authority
- `support-matrix.yaml`: 現行77 Tool Support Contract
- `release-verification.yaml`: v1.1.0 Stable Release Evidence

現在のOperational / Release状態は、上記Current EvidenceとManifest / Catalogを正本として判定します。

## Historical State

過去Releaseや過去Tool SurfaceのEvidenceは、current `main` に重複保存せず、Git historyと公開済みimmutable release tagsで参照します。過去RecordのRun ID、Artifact ID、当時のTool Countを現在状態の判定根拠には使用しません。

GitHub Actions runnerがStep実行前に停止した場合は`not_verified`です。Direct Unity Editor PASSはPrimary Evidenceですが、Target Device PASSや未実行CI PASSとしては扱いません。
