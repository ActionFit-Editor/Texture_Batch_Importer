# Texture Batch Importer (com.actionfit.texturebatchimporter)

텍스처/스프라이트 아틀라스/사운드의 **임포트 설정을 일괄 적용**하는 Unity 에디터 윈도우 모음입니다.

## 설치 (manifest.json, Git URL)

```json
{
  "dependencies": {
    "com.actionfit.texturebatchimporter": "https://github.com/ActionFit-Editor/Texture_Batch_Importer.git#1.0.5"
  }
}
```

## Agent Skills

After installing or updating the package, run `Tools > Package > Custom Package Manager > Install or Refresh Agent Skills`.

- `$texture-import-help`: explains texture, atlas, and sound batch workflows, menus, importer effects, and bulk-rewrite boundaries.

This package intentionally registers help only. The skill does not rewrite importers, invoke refresh or reimport, change presets, or modify project assets.

## Unity Menu

- Package root: `Tools > Package > Texture Batch Importer`.
- README: `Tools > Package > Texture Batch Importer > README`.
- Package commands stay under the same package root and appear above the separated README/Setting SO entries when those entries exist.

## 구성

- **Editor** (`com.actionfit.texturebatchimporter.Editor`):
  - `TextureBatchImporterWindow` — 메뉴 `Tools > Package > Texture Batch Importer > Texture Batch Importer`
  - `AtlasBatchImporterWindow` — 메뉴 `Tools > Package > Texture Batch Importer > Atlas Batch Importer`
  - `SoundBatchImporterWindow` — 메뉴 `Tools > Package > Texture Batch Importer > Sound Batch Importer`

## 사용

`Tools` 메뉴에서 각 윈도우를 열어 대상 폴더와 임포트 옵션을 지정 후 일괄 적용합니다.
