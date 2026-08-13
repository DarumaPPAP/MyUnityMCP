# Compatibility Evidence

## Current v1.1.0 Sources of Truth

- `stage2-8-main-merge-acceptance.yaml`: Unity `6000.7.0a2` Direct EditorでのExact 77 Tool Acceptance Evidence
- `stage2-8-validation-progress.yaml`: 77 Tool Promotion結果と残存`not_verified`範囲
- `editor-first-verification-policy.yaml`: Direct Unity Editor Primary / CI SupplementalのVerification Authority
- `support-matrix.yaml`: 現行77 Tool Support Contract
- `release-verification.yaml`: v1.1.0 Stable Release Evidence

## Historical Evidence

- `stage-0-production-baseline-verification.yaml`: 42 Tool時点の手動統合Evidence
- `world-creator-production-promotion-verification.yaml`: 45 Tool時点のWorldCreator Promotion Evidence
- `integration-hardening-verification.yaml`: v0.8 Hardening Evidence
- `apv-visual-acceptance-verification.yaml`: APV / Visual Acceptance実装時Evidence
- `capture-evidence-verification.yaml`: Capture実装時Evidence
- `verification-matrix.yaml`: 初期Implementation履歴

過去RecordのRun ID、Artifact ID、当時のTool Countは履歴として書き換えません。**現在のOperational / Release状態はSupport Matrix、Manifest、Catalog、77 Tool Acceptance、Release Verificationを正本**とします。

GitHub Actions runnerがStep実行前に停止した場合は`not_verified`です。Direct Unity Editor PASSはPrimary Evidenceですが、Target Device PASSや未実行CI PASSとしては扱いません。
