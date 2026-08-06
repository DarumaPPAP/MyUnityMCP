# Project Terminal Goal

## 達成状態

MyUnityMCPのPhase 0〜12に定義された採用Scopeをすべて実装・検証し、
UnityAgentMCPをControl Planeとして、採用Domain MCPとCreator Runtimeが統合動作する
Production Hardening済みRelease Candidateを完成させる。

## 必須成果物

- Release整合性とImmutable Release Policy
- UnityAgentMCP Runtime
- WorldCreator Runtime
- ProfilerMCP
- BuildMCP
- AddressablesMCP
- UIMCP
- AnimationMCP
- AudioMCP
- CinematicMCP
- MovieCreator
- LiveCreator
- Production Hardening
- Catalog／Manifest／Specs／Workflows／Docs／Tests／Evidenceの整合
- External MCP Client E2E
- Compatibility Matrix
- Security Mode
- Upgrade／Release Evidence

## Completion Gate

全体を`completed`にできるのは、次をすべて満たす場合だけ。

1. `bootstrap_development_harness`が完了
2. Phase 0〜12がすべてDone
3. 各Phaseの必須Evidenceが存在し、Schema検証に合格
4. UnityGraphicsMCPの回帰Testが合格
5. 実装状態とCatalog／Manifest／Docs表記が一致
6. Critical Safety Issueがない
7. Release Candidateが生成される
8. 人間が最終Releaseを承認する

## Non-terminal states

次はProject完了ではない。

- Phase完了
- PR作成
- PR Merge
- Design文書完成
- Catalogのstatus変更
- Testの一部成功
- Human Review待ち
- External Environment待ち

継続できない場合は`blocked`または`awaiting_approval`にする。
不足入力、完了済みCheckpoint、再開NodeをStateへ保存する。

## Quality and budget

成功率、Token、時間、費用の数値目標は未指定。
Codexは便宜的な数値を勝手に固定しない。

無人実行の反復上限は実行開始時にHarnessへ明示する。
未指定の場合、Harnessは無制限Loopを許可しない。
