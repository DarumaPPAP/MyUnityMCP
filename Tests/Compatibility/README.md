# Compatibility Evidence

- `stage-0-production-baseline-verification.yaml`: v1.1.0 / 42 Tool Production baselineのStage 0手動統合Evidence
- `release-verification.yaml`: 公開Release Gateの履歴Evidence
- `support-matrix.yaml`: 現行Support ContractとStage状態
- `integration-hardening-verification.yaml`: v0.8 Hardening Evidence
- `apv-visual-acceptance-verification.yaml`: APV／Visual Acceptance実装時Evidence
- `capture-evidence-verification.yaml`: Capture実装時Evidence
- `verification-matrix.yaml`: 初期Implementationの履歴

過去Recordの段階名、Run ID、Artifact IDは履歴として変更しません。現行対応を判断するときは`stage-0-production-baseline-verification.yaml`と`support-matrix.yaml`を優先し、公開済みReleaseの事実確認には`release-verification.yaml`を使用します。

GitHub Actions runnerがStep実行前に停止した場合、自動CIは`not_verified`のまま保持します。手動Unity Editor Evidenceは自動CIの代替合格として偽装せず、`integration_verified_manual`として分離して記録します。
