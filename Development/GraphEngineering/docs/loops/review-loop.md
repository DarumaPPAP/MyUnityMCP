# Review Loop

## Baseline

最初は単一Phase Loopを基準にする。
専門分野が複数あることだけでReviewer Agentを増やさない。

## Add a separate review node only when

- Self-reviewが同種欠陥を繰り返し見逃す
- Safety boundary regressionが観測される
- Catalog／Docs driftが再現する
- Product codeとTestが同じ誤解を共有する
- Human review負荷が高く、Agent reviewの改善が測定できる

## Review artifact

- Reviewed revision
- Applicable contracts
- Findings with severity
- Missing evidence
- False support claims
- Required fixes
- Verdict

会話要約をHandoff artifactにしない。

## Removal

Review nodeを外しても要求品質を満たすなら統合して単一Loopへ戻す。
