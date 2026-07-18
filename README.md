# Texture Batch Importer (com.actionfit.texturebatchimporter)

텍스처/스프라이트 아틀라스/사운드의 **임포트 설정을 일괄 적용**하는 Unity 에디터 윈도우 모음입니다.

## 설치 (manifest.json, Git URL)

```json
{
  "dependencies": {
    "com.actionfit.texturebatchimporter": "https://github.com/ActionFit-Editor/Texture_Batch_Importer.git#1.0.6"
  }
}
```

## Agent Skill 안내

패키지를 설치하거나 업데이트한 뒤 `Tools > Package > Custom Package Manager > Install or Refresh Agent Skills`를 실행합니다.

- `$texture-import-help`: texture, atlas 및 sound batch workflow, 메뉴, importer 영향과 bulk rewrite 경계를 설명합니다.

이 패키지는 의도적으로 help만 등록합니다. Skill은 importer rewrite, refresh/reimport 호출, preset 변경 또는 프로젝트 asset 수정을 실행하지 않습니다.

## Unity 메뉴

- Package root: `Tools > Package > Texture Batch Importer`.
- README: `Tools > Package > Texture Batch Importer > README`.
- 패키지 명령은 같은 package root 아래에 유지하며 README/Setting SO 항목이 있으면 분리된 해당 항목보다 위에 표시합니다.

## 구성

- **Editor** (`com.actionfit.texturebatchimporter.Editor`):
  - `TextureBatchImporterWindow` — 메뉴 `Tools > Package > Texture Batch Importer > Texture Batch Importer`
  - `AtlasBatchImporterWindow` — 메뉴 `Tools > Package > Texture Batch Importer > Atlas Batch Importer`
  - `SoundBatchImporterWindow` — 메뉴 `Tools > Package > Texture Batch Importer > Sound Batch Importer`

## 사용

`Tools` 메뉴에서 각 윈도우를 열어 대상 폴더와 임포트 옵션을 지정 후 일괄 적용합니다.
