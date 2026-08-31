# Sample Workflow

以下は安全なGraphics更新の標準順序です。

```text
get_support_matrix
→ inspect_project
→ inspect_scene
→ validate_scene
→ compile_direction
→ preview_plan
→ prepare_light_plan / prepare_environment_plan
→ Approval TokenとExact Diffを確認
→ apply_plan / apply_environment_plan
→ prepare_save_plan
→ apply_save_plan
→ prepare_bake_plan または prepare_apv_bake_plan
→ bake_dependencies または start_apv_bake + status polling
→ capture_evidence
→ prepare_acceptance_profile
→ evaluate_capture
→ submit_visual_review
→ refine_from_evaluation / refine_from_visual_review
```

## Rules

- Responseの`revision`が変化したら、後続Planを破棄してInspectへ戻ります。
- Applyの`saveMode`は`NONE`です。保存は別Toolで行います。
- BakeはScene保存後に別承認します。
- Evaluation MeasurementはUnity外で取得・判断した値を入力します。
- Human Reviewなしに完了としません。
