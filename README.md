# Texture Batch Importer (com.actionfit.texturebatchimporter)

텍스처/스프라이트 아틀라스의 **임포트 설정을 일괄 적용**하는 Unity 에디터 윈도우 모음입니다.

## 설치 (manifest.json, Git URL)

```json
{
  "dependencies": {
    "com.actionfit.texturebatchimporter": "https://github.com/ActionFit-Editor/Texture_Batch_Importer.git#1.0.0"
  }
}
```

## 구성

- **Editor** (`com.actionfit.texturebatchimporter.Editor`):
  - `TextureBatchImporterWindow` — 메뉴 `Tools > Texture Batch Importer`
  - `AtlasBatchImporterWindow` — 메뉴 `Tools > Atlas Batch Importer`

## 사용

`Tools` 메뉴에서 각 윈도우를 열어 대상 폴더와 임포트 옵션을 지정 후 일괄 적용합니다.
