#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public class AtlasBatchImporterWindow : EditorWindow
{
    #region Inner Types

    private struct ToggleValue<T>
    {
        public bool enabled; // 적용 여부
        public T value; // 설정 값

        public ToggleValue(T defaultValue)
        {
            enabled = false;
            value = defaultValue;
        }
    }

    private class PlatformSettings
    {
        public ToggleValue<bool> overrideForPlatform; // 플랫폼 오버라이드 활성화
        public ToggleValue<int> maxSize; // 최대 텍스처 크기
        public ToggleValue<TextureImporterFormat> format; // 압축 포맷
        public ToggleValue<TextureImporterCompression> compression; // 압축 방식
        public ToggleValue<int> compressionQuality; // 압축 품질 (0~100)

        public PlatformSettings()
        {
            overrideForPlatform = new ToggleValue<bool>(false);
            maxSize = new ToggleValue<int>(2048);
            format = new ToggleValue<TextureImporterFormat>(TextureImporterFormat.Automatic);
            compression = new ToggleValue<TextureImporterCompression>(TextureImporterCompression.Compressed);
            compressionQuality = new ToggleValue<int>(50);
        }
    }

    #endregion

    #region Fields

    private DefaultAsset _targetFolder; // 대상 폴더
    private List<string> _atlasPaths = new(); // 스캔된 아틀라스 경로 목록
    private Vector2 _scrollPosition; // 스크롤 위치

    // Texture Settings
    private ToggleValue<bool> _sRGB;
    private ToggleValue<bool> _readWrite;
    private ToggleValue<bool> _generateMipmaps;
    private ToggleValue<FilterMode> _filterMode;
    private ToggleValue<int> _anisoLevel;

    // Packing Settings
    private ToggleValue<int> _padding;
    private ToggleValue<bool> _allowRotation;
    private ToggleValue<bool> _tightPacking;
    private ToggleValue<bool> _alphaDilation;

    // Default Platform Settings
    private ToggleValue<int> _defaultMaxSize;
    private ToggleValue<TextureImporterFormat> _defaultFormat;
    private ToggleValue<TextureImporterCompression> _defaultCompression;
    private ToggleValue<int> _defaultCompressionQuality;

    // Android / iOS Platform Settings
    private PlatformSettings _androidSettings;
    private PlatformSettings _iosSettings;

    // Foldout 상태
    private bool _foldTexture = true;
    private bool _foldPacking = true;
    private bool _foldDefault = true;
    private bool _foldAndroid = true;
    private bool _foldIOS = true;

    // EditorPrefs 키 접두사
    private const string P = "AtlasBatchImporter.";

    // 유효한 Max Size 선택지
    private static readonly int[] MaxSizeOptions = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };
    private static readonly string[] MaxSizeLabels = { "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192", "16384" };

    // Padding 선택지
    private static readonly int[] PaddingOptions = { 2, 4, 8 };
    private static readonly string[] PaddingLabels = { "2", "4", "8" };

    // Compression Quality 선택지 (Fast=0, Normal=50, Best=100)
    private static readonly int[] CompressionQualityOptions = { 0, 50, 100 };
    private static readonly string[] CompressionQualityLabels = { "Fast", "Normal", "Best" };

    #endregion

    #region Properties

    private int CheckedSettingsCount => CountCheckedSettings();

    #endregion

    #region Window

    [MenuItem("Tools/Atlas Batch Importer")]
    public static void ShowWindow()
    {
        var window = GetWindow<AtlasBatchImporterWindow>("Atlas Batch Importer");
        window.minSize = new Vector2(420, 500);
        window.Show();
    }

    private void OnEnable()
    {
        _scrollPosition = Vector2.zero;
        _atlasPaths = new List<string>();
        InitializeDefaults();
        LoadAllPrefs();
    }

    private void OnDisable()
    {
        SaveAllPrefs();
    }

    #endregion

    #region Initialization

    // 모든 설정값을 기본값으로 초기화
    private void InitializeDefaults()
    {
        // Texture Settings
        _sRGB = new ToggleValue<bool>(true);
        _readWrite = new ToggleValue<bool>(false);
        _generateMipmaps = new ToggleValue<bool>(false);
        _filterMode = new ToggleValue<FilterMode>(FilterMode.Bilinear);
        _anisoLevel = new ToggleValue<int>(1);

        // Packing Settings
        _padding = new ToggleValue<int>(4);
        _allowRotation = new ToggleValue<bool>(false);
        _tightPacking = new ToggleValue<bool>(false);
        _alphaDilation = new ToggleValue<bool>(false);

        // Default Platform
        _defaultMaxSize = new ToggleValue<int>(2048);
        _defaultFormat = new ToggleValue<TextureImporterFormat>(TextureImporterFormat.Automatic);
        _defaultCompression = new ToggleValue<TextureImporterCompression>(TextureImporterCompression.Compressed);
        _defaultCompressionQuality = new ToggleValue<int>(50);

        // Android / iOS
        _androidSettings = new PlatformSettings();
        _iosSettings = new PlatformSettings();
    }

    #endregion

    #region GUI

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        DrawFolderSelection();

        EditorGUILayout.Space(5);

        DrawSelectButtons();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawTextureSettings();
        DrawPackingSettings();
        DrawDefaultPlatformSettings();
        DrawAndroidPlatformSettings();
        DrawIOSPlatformSettings();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(5);

        DrawApplyButton();

        EditorGUILayout.Space(5);
    }

    #endregion

    #region Draw Methods

    // 폴더 선택 및 스캔 영역
    private void DrawFolderSelection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Target Folder");

        _targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            _targetFolder,
            typeof(DefaultAsset),
            false
        );

        if (GUILayout.Button("Scan", GUILayout.Width(60)))
        {
            ScanFolder();
        }

        EditorGUILayout.EndHorizontal();

        if (_atlasPaths.Count > 0)
        {
            EditorGUILayout.HelpBox($"Found: {_atlasPaths.Count} atlases", MessageType.Info);
        }
    }

    // Select All / Deselect All 버튼
    private void DrawSelectButtons()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Select All"))
        {
            SetAllToggles(true);
        }

        if (GUILayout.Button("Deselect All"))
        {
            SetAllToggles(false);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
    }

    // Texture Settings 그룹
    private void DrawTextureSettings()
    {
        _foldTexture = EditorGUILayout.Foldout(_foldTexture, "Texture Settings", true, EditorStyles.foldoutHeader);
        if (!_foldTexture) return;

        EditorGUI.indentLevel++;

        DrawToggleBool("sRGB (Color Texture)", ref _sRGB);
        DrawToggleBool("Read/Write", ref _readWrite);
        DrawToggleBool("Generate Mipmaps", ref _generateMipmaps);
        DrawToggleEnum("Filter Mode", ref _filterMode);
        DrawToggleIntSlider("Aniso Level", ref _anisoLevel, 0, 16);

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
    }

    // Packing Settings 그룹
    private void DrawPackingSettings()
    {
        _foldPacking = EditorGUILayout.Foldout(_foldPacking, "Packing Settings", true, EditorStyles.foldoutHeader);
        if (!_foldPacking) return;

        EditorGUI.indentLevel++;

        DrawToggleIntPopup("Padding", ref _padding, PaddingOptions, PaddingLabels);
        DrawToggleBool("Allow Rotation", ref _allowRotation);
        DrawToggleBool("Tight Packing", ref _tightPacking);
        DrawToggleBool("Alpha Dilation", ref _alphaDilation);

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
    }

    // Default Platform Settings 그룹
    private void DrawDefaultPlatformSettings()
    {
        _foldDefault = EditorGUILayout.Foldout(_foldDefault, "Default Platform", true, EditorStyles.foldoutHeader);
        if (!_foldDefault) return;

        EditorGUI.indentLevel++;

        DrawToggleMaxSize("Max Size", ref _defaultMaxSize);
        DrawToggleEnum("Format", ref _defaultFormat);
        DrawToggleEnum("Compression", ref _defaultCompression);
        DrawToggleIntPopup("Compression Quality", ref _defaultCompressionQuality, CompressionQualityOptions, CompressionQualityLabels);

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
    }

    // Android Platform Settings 그룹
    private void DrawAndroidPlatformSettings()
    {
        _foldAndroid = EditorGUILayout.Foldout(_foldAndroid, "Android Platform", true, EditorStyles.foldoutHeader);
        if (!_foldAndroid) return;

        EditorGUI.indentLevel++;
        DrawPlatformSettings(_androidSettings);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
    }

    // iOS Platform Settings 그룹
    private void DrawIOSPlatformSettings()
    {
        _foldIOS = EditorGUILayout.Foldout(_foldIOS, "iOS Platform", true, EditorStyles.foldoutHeader);
        if (!_foldIOS) return;

        EditorGUI.indentLevel++;
        DrawPlatformSettings(_iosSettings);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
    }

    // 적용 버튼 및 경고 메시지 표시
    private void DrawApplyButton()
    {
        int checkedCount = CheckedSettingsCount;
        int totalAssets = _atlasPaths.Count;

        if (totalAssets > 0 && checkedCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"{totalAssets} atlases will be modified. ({checkedCount} settings checked)",
                MessageType.Warning
            );
        }

        GUI.enabled = totalAssets > 0 && checkedCount > 0;

        if (GUILayout.Button("Apply to All Atlases", GUILayout.Height(35)))
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Atlas Batch Importer",
                $"{totalAssets} atlases will be modified with {checkedCount} settings.\nProceed?",
                "Apply",
                "Cancel"
            );

            if (confirm)
            {
                ApplySettings();
            }
        }

        GUI.enabled = true;
    }

    #endregion

    #region Draw Helpers

    // Enum 타입 토글 필드
    private void DrawToggleEnum<T>(string label, ref ToggleValue<T> toggle) where T : System.Enum
    {
        EditorGUILayout.BeginHorizontal();
        toggle.enabled = GUILayout.Toggle(toggle.enabled, GUIContent.none, GUILayout.Width(15));

        GUI.enabled = toggle.enabled;
        toggle.value = (T)(object)EditorGUILayout.EnumPopup(label, toggle.value);
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    // Bool 타입 토글 필드
    private void DrawToggleBool(string label, ref ToggleValue<bool> toggle)
    {
        EditorGUILayout.BeginHorizontal();
        toggle.enabled = GUILayout.Toggle(toggle.enabled, GUIContent.none, GUILayout.Width(15));

        GUI.enabled = toggle.enabled;
        toggle.value = EditorGUILayout.Toggle(label, toggle.value);
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    // Int Slider 타입 토글 필드
    private void DrawToggleIntSlider(string label, ref ToggleValue<int> toggle, int min, int max)
    {
        EditorGUILayout.BeginHorizontal();
        toggle.enabled = GUILayout.Toggle(toggle.enabled, GUIContent.none, GUILayout.Width(15));

        GUI.enabled = toggle.enabled;
        toggle.value = EditorGUILayout.IntSlider(label, toggle.value, min, max);
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    // Max Size 전용 토글 필드 (Popup)
    private void DrawToggleMaxSize(string label, ref ToggleValue<int> toggle)
    {
        EditorGUILayout.BeginHorizontal();
        toggle.enabled = GUILayout.Toggle(toggle.enabled, GUIContent.none, GUILayout.Width(15));

        GUI.enabled = toggle.enabled;
        int currentIndex = System.Array.IndexOf(MaxSizeOptions, toggle.value);
        if (currentIndex < 0) currentIndex = 6; // 기본값 2048
        int selectedIndex = EditorGUILayout.Popup(label, currentIndex, MaxSizeLabels);
        toggle.value = MaxSizeOptions[selectedIndex];
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    // Int Popup 타입 토글 필드 (고정 선택지)
    private void DrawToggleIntPopup(string label, ref ToggleValue<int> toggle, int[] options, string[] labels)
    {
        EditorGUILayout.BeginHorizontal();
        toggle.enabled = GUILayout.Toggle(toggle.enabled, GUIContent.none, GUILayout.Width(15));

        GUI.enabled = toggle.enabled;
        int currentIndex = System.Array.IndexOf(options, toggle.value);
        if (currentIndex < 0) currentIndex = 0;
        int selectedIndex = EditorGUILayout.Popup(label, currentIndex, labels);
        toggle.value = options[selectedIndex];
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    // Platform Settings 공통 그리기
    private void DrawPlatformSettings(PlatformSettings settings)
    {
        DrawToggleBool("Override", ref settings.overrideForPlatform);
        DrawToggleMaxSize("Max Size", ref settings.maxSize);
        DrawToggleEnum("Format", ref settings.format);
        DrawToggleEnum("Compression", ref settings.compression);
        DrawToggleIntPopup("Compression Quality", ref settings.compressionQuality, CompressionQualityOptions, CompressionQualityLabels);
    }

    #endregion

    #region Private Methods

    // 대상 폴더에서 Sprite Atlas 파일 스캔
    private void ScanFolder()
    {
        _atlasPaths.Clear();

        if (_targetFolder == null)
        {
            Debug.LogWarning("[AtlasBatchImporter] No target folder selected");
            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(_targetFolder);
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError("[AtlasBatchImporter] Invalid folder path: " + folderPath);
            return;
        }

        string[] atlasExtensions = { "*.spriteatlas", "*.spriteatlasv2" };
        foreach (string ext in atlasExtensions)
        {
            string[] atlasFiles = Directory.GetFiles(folderPath, ext, SearchOption.AllDirectories);
            foreach (string file in atlasFiles)
            {
                string assetPath = file.Replace("\\", "/");
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(assetPath);
                if (atlas != null)
                {
                    _atlasPaths.Add(assetPath);
                }
            }
        }

        Debug.Log($"[AtlasBatchImporter] Scanned {_atlasPaths.Count} atlases in: {folderPath}");
    }

    // 체크된 설정을 모든 아틀라스에 일괄 적용
    private void ApplySettings()
    {
        int total = _atlasPaths.Count;
        int processed = 0;
        bool cancelled = false;

        for (int i = 0; i < total; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar(
                "Atlas Batch Importer",
                $"Applying to atlases... ({i + 1}/{total})",
                (float)i / total))
            {
                cancelled = true;
                break;
            }

            string path = _atlasPaths[i];
            bool isV2 = path.EndsWith(".spriteatlasv2");

            if (isV2)
            {
                ApplyToAtlasV2(path);
            }
            else
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (atlas == null) continue;

                ApplyToAtlasV1(atlas);
                EditorUtility.SetDirty(atlas);
            }

            processed++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();

        string result = cancelled
            ? $"Cancelled. Applied to {processed}/{total} atlases."
            : $"Completed. Applied to {processed} atlases.";
        Debug.Log($"[AtlasBatchImporter] {result}");
        EditorUtility.DisplayDialog("Atlas Batch Importer", result, "OK");
    }

    // v1 (.spriteatlas): SpriteAtlasExtensions API로 설정 적용
    private void ApplyToAtlasV1(SpriteAtlas atlas)
    {
        // Texture Settings
        if (_sRGB.enabled || _readWrite.enabled || _generateMipmaps.enabled ||
            _filterMode.enabled || _anisoLevel.enabled)
        {
            var texSettings = atlas.GetTextureSettings();
            if (_sRGB.enabled) texSettings.sRGB = _sRGB.value;
            if (_readWrite.enabled) texSettings.readable = _readWrite.value;
            if (_generateMipmaps.enabled) texSettings.generateMipMaps = _generateMipmaps.value;
            if (_filterMode.enabled) texSettings.filterMode = _filterMode.value;
            if (_anisoLevel.enabled) texSettings.anisoLevel = _anisoLevel.value;
            atlas.SetTextureSettings(texSettings);
        }

        // Packing Settings
        if (_padding.enabled || _allowRotation.enabled || _tightPacking.enabled || _alphaDilation.enabled)
        {
            var packSettings = atlas.GetPackingSettings();
            if (_padding.enabled) packSettings.padding = _padding.value;
            if (_allowRotation.enabled) packSettings.enableRotation = _allowRotation.value;
            if (_tightPacking.enabled) packSettings.enableTightPacking = _tightPacking.value;
            if (_alphaDilation.enabled) packSettings.enableAlphaDilation = _alphaDilation.value;
            atlas.SetPackingSettings(packSettings);
        }

        // Default Platform
        if (_defaultMaxSize.enabled || _defaultFormat.enabled ||
            _defaultCompression.enabled || _defaultCompressionQuality.enabled)
        {
            var defaultSettings = atlas.GetPlatformSettings("DefaultTexturePlatform");
            if (_defaultMaxSize.enabled) defaultSettings.maxTextureSize = _defaultMaxSize.value;
            if (_defaultFormat.enabled) defaultSettings.format = _defaultFormat.value;
            if (_defaultCompression.enabled) defaultSettings.textureCompression = _defaultCompression.value;
            if (_defaultCompressionQuality.enabled) defaultSettings.compressionQuality = _defaultCompressionQuality.value;
            defaultSettings.overridden = true;
            atlas.SetPlatformSettings(defaultSettings);
        }

        // Android / iOS
        ApplyPlatformSettingsV1(atlas, "Android", _androidSettings);
        ApplyPlatformSettingsV1(atlas, "iPhone", _iosSettings);
    }

    // v1: Sprite Atlas 플랫폼별 설정 적용
    private void ApplyPlatformSettingsV1(SpriteAtlas atlas, string platform, PlatformSettings settings)
    {
        bool hasAnyEnabled = settings.overrideForPlatform.enabled ||
                             settings.maxSize.enabled ||
                             settings.format.enabled ||
                             settings.compression.enabled ||
                             settings.compressionQuality.enabled;

        if (!hasAnyEnabled) return;

        var platformSettings = atlas.GetPlatformSettings(platform);

        if (settings.overrideForPlatform.enabled)
            platformSettings.overridden = settings.overrideForPlatform.value;

        if (settings.maxSize.enabled)
            platformSettings.maxTextureSize = settings.maxSize.value;

        if (settings.format.enabled)
            platformSettings.format = settings.format.value;

        if (settings.compression.enabled)
            platformSettings.textureCompression = settings.compression.value;

        if (settings.compressionQuality.enabled)
            platformSettings.compressionQuality = settings.compressionQuality.value;

        atlas.SetPlatformSettings(platformSettings);
    }

    // v2 (.spriteatlasv2): SpriteAtlasImporter API로 설정 적용
    private void ApplyToAtlasV2(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as SpriteAtlasImporter;
        if (importer == null) return;

        // Texture Settings
        if (_sRGB.enabled || _readWrite.enabled || _generateMipmaps.enabled ||
            _filterMode.enabled || _anisoLevel.enabled)
        {
            var texSettings = importer.textureSettings;
            if (_sRGB.enabled) texSettings.sRGB = _sRGB.value;
            if (_readWrite.enabled) texSettings.readable = _readWrite.value;
            if (_generateMipmaps.enabled) texSettings.generateMipMaps = _generateMipmaps.value;
            if (_filterMode.enabled) texSettings.filterMode = _filterMode.value;
            if (_anisoLevel.enabled) texSettings.anisoLevel = _anisoLevel.value;
            importer.textureSettings = texSettings;
        }

        // Packing Settings
        if (_padding.enabled || _allowRotation.enabled || _tightPacking.enabled || _alphaDilation.enabled)
        {
            var packSettings = importer.packingSettings;
            if (_padding.enabled) packSettings.padding = _padding.value;
            if (_allowRotation.enabled) packSettings.enableRotation = _allowRotation.value;
            if (_tightPacking.enabled) packSettings.enableTightPacking = _tightPacking.value;
            if (_alphaDilation.enabled) packSettings.enableAlphaDilation = _alphaDilation.value;
            importer.packingSettings = packSettings;
        }

        // Default Platform
        if (_defaultMaxSize.enabled || _defaultFormat.enabled ||
            _defaultCompression.enabled || _defaultCompressionQuality.enabled)
        {
            var defaultSettings = importer.GetPlatformSettings("DefaultTexturePlatform");
            if (_defaultMaxSize.enabled) defaultSettings.maxTextureSize = _defaultMaxSize.value;
            if (_defaultFormat.enabled) defaultSettings.format = _defaultFormat.value;
            if (_defaultCompression.enabled) defaultSettings.textureCompression = _defaultCompression.value;
            if (_defaultCompressionQuality.enabled) defaultSettings.compressionQuality = _defaultCompressionQuality.value;
            defaultSettings.overridden = true;
            importer.SetPlatformSettings(defaultSettings);
        }

        // Android / iOS
        ApplyPlatformSettingsV2(importer, "Android", _androidSettings);
        ApplyPlatformSettingsV2(importer, "iPhone", _iosSettings);

        importer.SaveAndReimport();
    }

    // v2: SpriteAtlasImporter 플랫폼별 설정 적용 (Android/iOS)
    private void ApplyPlatformSettingsV2(SpriteAtlasImporter importer, string platform, PlatformSettings settings)
    {
        bool hasAnyEnabled = settings.overrideForPlatform.enabled ||
                             settings.maxSize.enabled ||
                             settings.format.enabled ||
                             settings.compression.enabled ||
                             settings.compressionQuality.enabled;

        if (!hasAnyEnabled) return;

        var platformSettings = importer.GetPlatformSettings(platform);

        if (settings.overrideForPlatform.enabled)
            platformSettings.overridden = settings.overrideForPlatform.value;

        if (settings.maxSize.enabled)
            platformSettings.maxTextureSize = settings.maxSize.value;

        if (settings.format.enabled)
            platformSettings.format = settings.format.value;

        if (settings.compression.enabled)
            platformSettings.textureCompression = settings.compression.value;

        if (settings.compressionQuality.enabled)
            platformSettings.compressionQuality = settings.compressionQuality.value;

        importer.SetPlatformSettings(platformSettings);
    }

    #endregion

    #region EditorPrefs

    // EditorPrefs 저장 헬퍼
    private void SaveBool(string key, bool value) => EditorPrefs.SetBool(P + key, value);
    private void SaveInt(string key, int value) => EditorPrefs.SetInt(P + key, value);

    // EditorPrefs 로드 헬퍼
    private bool LoadBool(string key, bool def) => EditorPrefs.GetBool(P + key, def);
    private int LoadInt(string key, int def) => EditorPrefs.GetInt(P + key, def);

    // 모든 설정을 EditorPrefs에 저장
    private void SaveAllPrefs()
    {
        // Target Folder
        SaveString("folder", _targetFolder != null ? AssetDatabase.GetAssetPath(_targetFolder) : "");

        // Texture Settings
        SaveBool("sRGB.on", _sRGB.enabled);
        SaveBool("sRGB.v", _sRGB.value);
        SaveBool("readWrite.on", _readWrite.enabled);
        SaveBool("readWrite.v", _readWrite.value);
        SaveBool("generateMipmaps.on", _generateMipmaps.enabled);
        SaveBool("generateMipmaps.v", _generateMipmaps.value);
        SaveBool("filterMode.on", _filterMode.enabled);
        SaveInt("filterMode.v", (int)_filterMode.value);
        SaveBool("anisoLevel.on", _anisoLevel.enabled);
        SaveInt("anisoLevel.v", _anisoLevel.value);

        // Packing Settings
        SaveBool("padding.on", _padding.enabled);
        SaveInt("padding.v", _padding.value);
        SaveBool("allowRotation.on", _allowRotation.enabled);
        SaveBool("allowRotation.v", _allowRotation.value);
        SaveBool("tightPacking.on", _tightPacking.enabled);
        SaveBool("tightPacking.v", _tightPacking.value);
        SaveBool("alphaDilation.on", _alphaDilation.enabled);
        SaveBool("alphaDilation.v", _alphaDilation.value);

        // Default Platform
        SaveBool("defaultMaxSize.on", _defaultMaxSize.enabled);
        SaveInt("defaultMaxSize.v", _defaultMaxSize.value);
        SaveBool("defaultFormat.on", _defaultFormat.enabled);
        SaveInt("defaultFormat.v", (int)_defaultFormat.value);
        SaveBool("defaultCompression.on", _defaultCompression.enabled);
        SaveInt("defaultCompression.v", (int)_defaultCompression.value);
        SaveBool("defaultCompressionQuality.on", _defaultCompressionQuality.enabled);
        SaveInt("defaultCompressionQuality.v", _defaultCompressionQuality.value);

        // Android / iOS
        SavePlatformPrefs("android", _androidSettings);
        SavePlatformPrefs("ios", _iosSettings);

        // Foldout 상태
        SaveBool("fold.texture", _foldTexture);
        SaveBool("fold.packing", _foldPacking);
        SaveBool("fold.default", _foldDefault);
        SaveBool("fold.android", _foldAndroid);
        SaveBool("fold.ios", _foldIOS);
    }

    // EditorPrefs에서 모든 설정 복원
    private void LoadAllPrefs()
    {
        // Target Folder
        string folderPath = LoadString("folder", "");
        if (!string.IsNullOrEmpty(folderPath))
            _targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);

        // Texture Settings
        _sRGB.enabled = LoadBool("sRGB.on", _sRGB.enabled);
        _sRGB.value = LoadBool("sRGB.v", _sRGB.value);
        _readWrite.enabled = LoadBool("readWrite.on", _readWrite.enabled);
        _readWrite.value = LoadBool("readWrite.v", _readWrite.value);
        _generateMipmaps.enabled = LoadBool("generateMipmaps.on", _generateMipmaps.enabled);
        _generateMipmaps.value = LoadBool("generateMipmaps.v", _generateMipmaps.value);
        _filterMode.enabled = LoadBool("filterMode.on", _filterMode.enabled);
        _filterMode.value = (FilterMode)LoadInt("filterMode.v", (int)_filterMode.value);
        _anisoLevel.enabled = LoadBool("anisoLevel.on", _anisoLevel.enabled);
        _anisoLevel.value = LoadInt("anisoLevel.v", _anisoLevel.value);

        // Packing Settings
        _padding.enabled = LoadBool("padding.on", _padding.enabled);
        _padding.value = LoadInt("padding.v", _padding.value);
        _allowRotation.enabled = LoadBool("allowRotation.on", _allowRotation.enabled);
        _allowRotation.value = LoadBool("allowRotation.v", _allowRotation.value);
        _tightPacking.enabled = LoadBool("tightPacking.on", _tightPacking.enabled);
        _tightPacking.value = LoadBool("tightPacking.v", _tightPacking.value);
        _alphaDilation.enabled = LoadBool("alphaDilation.on", _alphaDilation.enabled);
        _alphaDilation.value = LoadBool("alphaDilation.v", _alphaDilation.value);

        // Default Platform
        _defaultMaxSize.enabled = LoadBool("defaultMaxSize.on", _defaultMaxSize.enabled);
        _defaultMaxSize.value = LoadInt("defaultMaxSize.v", _defaultMaxSize.value);
        _defaultFormat.enabled = LoadBool("defaultFormat.on", _defaultFormat.enabled);
        _defaultFormat.value = (TextureImporterFormat)LoadInt("defaultFormat.v", (int)_defaultFormat.value);
        _defaultCompression.enabled = LoadBool("defaultCompression.on", _defaultCompression.enabled);
        _defaultCompression.value = (TextureImporterCompression)LoadInt("defaultCompression.v", (int)_defaultCompression.value);
        _defaultCompressionQuality.enabled = LoadBool("defaultCompressionQuality.on", _defaultCompressionQuality.enabled);
        _defaultCompressionQuality.value = LoadInt("defaultCompressionQuality.v", _defaultCompressionQuality.value);

        // Android / iOS
        LoadPlatformPrefs("android", _androidSettings);
        LoadPlatformPrefs("ios", _iosSettings);

        // Foldout 상태
        _foldTexture = LoadBool("fold.texture", _foldTexture);
        _foldPacking = LoadBool("fold.packing", _foldPacking);
        _foldDefault = LoadBool("fold.default", _foldDefault);
        _foldAndroid = LoadBool("fold.android", _foldAndroid);
        _foldIOS = LoadBool("fold.ios", _foldIOS);
    }

    // 플랫폼 설정 저장
    private void SavePlatformPrefs(string prefix, PlatformSettings s)
    {
        SaveBool($"{prefix}.override.on", s.overrideForPlatform.enabled);
        SaveBool($"{prefix}.override.v", s.overrideForPlatform.value);
        SaveBool($"{prefix}.maxSize.on", s.maxSize.enabled);
        SaveInt($"{prefix}.maxSize.v", s.maxSize.value);
        SaveBool($"{prefix}.format.on", s.format.enabled);
        SaveInt($"{prefix}.format.v", (int)s.format.value);
        SaveBool($"{prefix}.compression.on", s.compression.enabled);
        SaveInt($"{prefix}.compression.v", (int)s.compression.value);
        SaveBool($"{prefix}.compressionQuality.on", s.compressionQuality.enabled);
        SaveInt($"{prefix}.compressionQuality.v", s.compressionQuality.value);
    }

    // 플랫폼 설정 복원
    private void LoadPlatformPrefs(string prefix, PlatformSettings s)
    {
        s.overrideForPlatform.enabled = LoadBool($"{prefix}.override.on", s.overrideForPlatform.enabled);
        s.overrideForPlatform.value = LoadBool($"{prefix}.override.v", s.overrideForPlatform.value);
        s.maxSize.enabled = LoadBool($"{prefix}.maxSize.on", s.maxSize.enabled);
        s.maxSize.value = LoadInt($"{prefix}.maxSize.v", s.maxSize.value);
        s.format.enabled = LoadBool($"{prefix}.format.on", s.format.enabled);
        s.format.value = (TextureImporterFormat)LoadInt($"{prefix}.format.v", (int)s.format.value);
        s.compression.enabled = LoadBool($"{prefix}.compression.on", s.compression.enabled);
        s.compression.value = (TextureImporterCompression)LoadInt($"{prefix}.compression.v", (int)s.compression.value);
        s.compressionQuality.enabled = LoadBool($"{prefix}.compressionQuality.on", s.compressionQuality.enabled);
        s.compressionQuality.value = LoadInt($"{prefix}.compressionQuality.v", s.compressionQuality.value);
    }

    // EditorPrefs String 저장/로드 헬퍼
    private void SaveString(string key, string value) => EditorPrefs.SetString(P + key, value ?? "");
    private string LoadString(string key, string def) => EditorPrefs.GetString(P + key, def ?? "");

    #endregion

    #region Utility Methods

    // 모든 토글을 켜거나 끄기
    private void SetAllToggles(bool enabled)
    {
        // Texture Settings
        _sRGB.enabled = enabled;
        _readWrite.enabled = enabled;
        _generateMipmaps.enabled = enabled;
        _filterMode.enabled = enabled;
        _anisoLevel.enabled = enabled;

        // Packing Settings
        _padding.enabled = enabled;
        _allowRotation.enabled = enabled;
        _tightPacking.enabled = enabled;
        _alphaDilation.enabled = enabled;

        // Default Platform
        _defaultMaxSize.enabled = enabled;
        _defaultFormat.enabled = enabled;
        _defaultCompression.enabled = enabled;
        _defaultCompressionQuality.enabled = enabled;

        // Android
        _androidSettings.overrideForPlatform.enabled = enabled;
        _androidSettings.maxSize.enabled = enabled;
        _androidSettings.format.enabled = enabled;
        _androidSettings.compression.enabled = enabled;
        _androidSettings.compressionQuality.enabled = enabled;

        // iOS
        _iosSettings.overrideForPlatform.enabled = enabled;
        _iosSettings.maxSize.enabled = enabled;
        _iosSettings.format.enabled = enabled;
        _iosSettings.compression.enabled = enabled;
        _iosSettings.compressionQuality.enabled = enabled;
    }

    // 체크된 설정 항목 수 계산
    private int CountCheckedSettings()
    {
        int count = 0;

        // Texture Settings (5)
        if (_sRGB.enabled) count++;
        if (_readWrite.enabled) count++;
        if (_generateMipmaps.enabled) count++;
        if (_filterMode.enabled) count++;
        if (_anisoLevel.enabled) count++;

        // Packing Settings (4)
        if (_padding.enabled) count++;
        if (_allowRotation.enabled) count++;
        if (_tightPacking.enabled) count++;
        if (_alphaDilation.enabled) count++;

        // Default Platform (4)
        if (_defaultMaxSize.enabled) count++;
        if (_defaultFormat.enabled) count++;
        if (_defaultCompression.enabled) count++;
        if (_defaultCompressionQuality.enabled) count++;

        // Android (5)
        if (_androidSettings.overrideForPlatform.enabled) count++;
        if (_androidSettings.maxSize.enabled) count++;
        if (_androidSettings.format.enabled) count++;
        if (_androidSettings.compression.enabled) count++;
        if (_androidSettings.compressionQuality.enabled) count++;

        // iOS (5)
        if (_iosSettings.overrideForPlatform.enabled) count++;
        if (_iosSettings.maxSize.enabled) count++;
        if (_iosSettings.format.enabled) count++;
        if (_iosSettings.compression.enabled) count++;
        if (_iosSettings.compressionQuality.enabled) count++;

        return count;
    }

    #endregion
}

#endif
