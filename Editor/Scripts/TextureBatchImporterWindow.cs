#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
#pragma warning disable CS0618 // 형식 또는 멤버는 사용되지 않습니다.

public class TextureBatchImporterWindow : EditorWindow
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
        public bool useDefaultMaxSize; // Default Platform의 MaxSize 사용 여부
        public ToggleValue<TextureImporterFormat> format; // 압축 포맷
        public ToggleValue<TextureImporterCompression> compression; // 압축 방식
        public ToggleValue<int> compressionQuality; // 압축 품질 (0~100)

        public PlatformSettings()
        {
            overrideForPlatform = new ToggleValue<bool>(false);
            maxSize = new ToggleValue<int>(2048);
            useDefaultMaxSize = false;
            format = new ToggleValue<TextureImporterFormat>(TextureImporterFormat.Automatic);
            compression = new ToggleValue<TextureImporterCompression>(TextureImporterCompression.Compressed);
            compressionQuality = new ToggleValue<int>(50);
        }
    }

    #endregion

    #region Fields

    private List<DefaultAsset> _targetFolders = new(); // 대상 폴더 목록
    private List<Texture2D> _targetTextures = new(); // 대상 개별 텍스처 목록
    private List<string> _texturePaths = new(); // 스캔된 텍스처 경로 목록
    private Texture2D _referenceTexture; // 설정값 복사 원본 (Apply 대상에서 제외 — 슬롯에 드롭 시 모든 토글 자동 ON + 값 덮어쓰기)
    private Vector2 _scrollPosition; // 스크롤 위치

    // General Settings
    private ToggleValue<TextureImporterType> _textureType;
    private ToggleValue<TextureImporterShape> _textureShape;
    private ToggleValue<bool> _sRGB;
    private ToggleValue<TextureImporterAlphaSource> _alphaSource;
    private ToggleValue<bool> _alphaIsTransparency;
    private ToggleValue<bool> _readWrite;
    private ToggleValue<bool> _generateMipmaps;
    private ToggleValue<TextureWrapMode> _wrapMode;
    private ToggleValue<FilterMode> _filterMode;
    private ToggleValue<int> _anisoLevel;

    // Sprite Settings
    private ToggleValue<SpriteImportMode> _spriteMode;
    private ToggleValue<string> _packingTag;
    private ToggleValue<float> _pixelsPerUnit;
    private ToggleValue<SpriteMeshType> _meshType;
    private ToggleValue<bool> _generatePhysicsShape;
    private ToggleValue<uint> _extrudeEdges;

    // Default Platform Settings
    private ToggleValue<int> _defaultMaxSize;
    private ToggleValue<TextureImporterFormat> _defaultFormat;
    private ToggleValue<TextureImporterCompression> _defaultCompression;
    private ToggleValue<int> _defaultCompressionQuality;

    // Android / iOS Platform Settings
    private PlatformSettings _androidSettings;
    private PlatformSettings _iosSettings;

    // Foldout 상태
    private bool _foldGeneral = true;
    private bool _foldSprite = true;
    private bool _foldDefault = true;
    private bool _foldAndroid = true;
    private bool _foldIOS = true;

    // 유효한 Max Size 선택지
    private static readonly int[] MaxSizeOptions = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };
    private static readonly string[] MaxSizeLabels = { "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192", "16384" };

    // Compression Quality 선택지 (Fast=0, Normal=50, Best=100)
    private static readonly int[] CompressionQualityOptions = { 0, 50, 100 };
    private static readonly string[] CompressionQualityLabels = { "Fast", "Normal", "Best" };

    // EditorPrefs 키 접두사
    private const string P = "TextureBatchImporter.";

    #endregion

    #region Properties

    private int CheckedSettingsCount => CountCheckedSettings();

    #endregion

    #region Window

    [MenuItem("Tools/Texture Batch Importer")]
    public static void ShowWindow()
    {
        var window = GetWindow<TextureBatchImporterWindow>("Texture Batch Importer");
        window.minSize = new Vector2(420, 500);
        window.Show();
    }

    private void OnEnable()
    {
        _scrollPosition = Vector2.zero;
        _targetFolders = new List<DefaultAsset>();
        _texturePaths = new List<string>();
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
        // General
        _textureType = new ToggleValue<TextureImporterType>(TextureImporterType.Sprite);
        _textureShape = new ToggleValue<TextureImporterShape>(TextureImporterShape.Texture2D);
        _sRGB = new ToggleValue<bool>(true);
        _alphaSource = new ToggleValue<TextureImporterAlphaSource>(TextureImporterAlphaSource.FromInput);
        _alphaIsTransparency = new ToggleValue<bool>(true);
        _readWrite = new ToggleValue<bool>(false);
        _generateMipmaps = new ToggleValue<bool>(false);
        _wrapMode = new ToggleValue<TextureWrapMode>(TextureWrapMode.Clamp);
        _filterMode = new ToggleValue<FilterMode>(FilterMode.Bilinear);
        _anisoLevel = new ToggleValue<int>(1);

        // Sprite
        _spriteMode = new ToggleValue<SpriteImportMode>(SpriteImportMode.Single);
        _packingTag = new ToggleValue<string>("");
        _pixelsPerUnit = new ToggleValue<float>(100f);
        _meshType = new ToggleValue<SpriteMeshType>(SpriteMeshType.Tight);
        _generatePhysicsShape = new ToggleValue<bool>(false);
        _extrudeEdges = new ToggleValue<uint>(1);

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

        DrawTextureSelection();

        EditorGUILayout.Space(5);

        DrawSelectButtons();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawGeneralSettings();
        DrawSpriteSettings();
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

    // 폴더 선택 영역 (복수 폴더 지원, 드래그 앤 드롭 + 개별 관리)
    private void DrawFolderSelection()
    {
        EditorGUILayout.LabelField("Target Folders", EditorStyles.boldLabel);

        DrawFolderDropArea();

        bool needsRescan = false;
        bool showDialog = false;

        for (int i = 0; i < _targetFolders.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            var newFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                _targetFolders[i],
                typeof(DefaultAsset),
                false
            );

            if (newFolder != _targetFolders[i])
            {
                _targetFolders[i] = newFolder;
                needsRescan = true;
                if (newFolder != null) showDialog = true;
            }

            if (GUILayout.Button("X", GUILayout.Width(22)))
            {
                _targetFolders.RemoveAt(i);
                needsRescan = true;
                i--;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (_targetFolders.Count > 0 && GUILayout.Button("Remove All"))
        {
            _targetFolders.Clear();
            _texturePaths.Clear();
        }

        if (needsRescan)
        {
            ScanAll();

            if (showDialog && _texturePaths.Count > 0)
            {
                int folderCount = _targetFolders.FindAll(f => f != null).Count;
                EditorUtility.DisplayDialog(
                    "Texture Batch Importer",
                    $"{_texturePaths.Count} sprites found across {folderCount} folder(s).",
                    "OK"
                );
            }
        }
    }

    // 폴더 드래그 앤 드롭 영역
    private void DrawFolderDropArea()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0, 35, GUILayout.ExpandWidth(true));

        var style = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11
        };
        GUI.Box(dropArea, "Drag & Drop Folders Here", style);

        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition)) return;

        if (evt.type == EventType.DragUpdated)
        {
            foreach (var obj in DragAndDrop.objectReferences)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (obj is DefaultAsset && Directory.Exists(path))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                    return;
                }
            }
        }
        else if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();

            int addedCount = 0;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is not DefaultAsset folder) continue;

                string path = AssetDatabase.GetAssetPath(folder);
                if (!Directory.Exists(path)) continue;
                if (_targetFolders.Contains(folder)) continue;

                _targetFolders.Add(folder);
                addedCount++;
            }

            if (addedCount > 0)
            {
                ScanAll();
                int folderCount = _targetFolders.FindAll(f => f != null).Count;
                EditorUtility.DisplayDialog(
                    "Texture Batch Importer",
                    $"{_texturePaths.Count} sprites found across {folderCount} folder(s).\n({addedCount} folder(s) added)",
                    "OK"
                );
            }

            evt.Use();
        }
    }

    // 개별 텍스처 선택 영역
    private void DrawTextureSelection()
    {
        EditorGUILayout.LabelField("Individual Textures", EditorStyles.boldLabel);

        DrawTextureDropArea();

        bool needsRescan = false;

        for (int i = 0; i < _targetTextures.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            var newTex = (Texture2D)EditorGUILayout.ObjectField(
                _targetTextures[i],
                typeof(Texture2D),
                false
            );

            if (newTex != _targetTextures[i])
            {
                _targetTextures[i] = newTex;
                needsRescan = true;
            }

            if (GUILayout.Button("X", GUILayout.Width(22)))
            {
                _targetTextures.RemoveAt(i);
                needsRescan = true;
                i--;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (_targetTextures.Count > 0 && GUILayout.Button("Remove All Textures"))
        {
            _targetTextures.Clear();
            needsRescan = true;
        }

        if (needsRescan) ScanAll();
    }

    // 텍스처 드래그 앤 드롭 영역
    private void DrawTextureDropArea()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0, 35, GUILayout.ExpandWidth(true));

        var style = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11
        };
        GUI.Box(dropArea, "Drag & Drop Textures Here", style);

        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition)) return;

        if (evt.type == EventType.DragUpdated)
        {
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is Texture2D)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                    return;
                }
            }
        }
        else if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();

            int addedCount = 0;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is not Texture2D tex) continue;
                if (_targetTextures.Contains(tex)) continue;

                _targetTextures.Add(tex);
                addedCount++;
            }

            if (addedCount > 0)
            {
                ScanAll();
                EditorUtility.DisplayDialog(
                    "Texture Batch Importer",
                    $"{addedCount} texture(s) added. Total: {_texturePaths.Count} textures.",
                    "OK"
                );
            }

            evt.Use();
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

    // General Settings 그룹
    private void DrawGeneralSettings()
    {
        _foldGeneral = EditorGUILayout.Foldout(_foldGeneral, "General Settings", true, EditorStyles.foldoutHeader);
        if (!_foldGeneral) return;

        EditorGUI.indentLevel++;

        DrawToggleEnum("Texture Type", ref _textureType);
        DrawToggleEnum("Shape", ref _textureShape);
        DrawToggleBool("sRGB (Color Texture)", ref _sRGB);
        DrawToggleEnum("Alpha Source", ref _alphaSource);
        DrawToggleBool("Alpha Is Transparency", ref _alphaIsTransparency);
        DrawToggleBool("Read/Write", ref _readWrite);
        DrawToggleBool("Generate Mipmaps", ref _generateMipmaps);
        DrawToggleEnum("Wrap Mode", ref _wrapMode);
        DrawToggleEnum("Filter Mode", ref _filterMode);
        DrawToggleIntSlider("Aniso Level", ref _anisoLevel, 0, 16);

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
    }

    // Sprite Settings 그룹
    private void DrawSpriteSettings()
    {
        _foldSprite = EditorGUILayout.Foldout(_foldSprite, "Sprite Settings", true, EditorStyles.foldoutHeader);
        if (!_foldSprite) return;

        EditorGUI.indentLevel++;

        DrawToggleEnum("Sprite Mode", ref _spriteMode);
        DrawToggleString("Packing Tag", ref _packingTag);
        DrawToggleFloat("Pixels Per Unit", ref _pixelsPerUnit);
        DrawToggleEnum("Mesh Type", ref _meshType);
        DrawToggleBool("Generate Physics Shape", ref _generatePhysicsShape);
        DrawToggleUint("Extrude Edges", ref _extrudeEdges, 0, 32);

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

    // 적용 버튼 및 경고 메시지 표시 (우측에 Reference 슬롯 동반)
    private void DrawApplyButton()
    {
        int checkedCount = CheckedSettingsCount;

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = _targetFolders.Exists(f => f != null) || _targetTextures.Exists(t => t != null);

        if (GUILayout.Button("Apply to All Textures", GUILayout.Height(35)))
        {
            // 스캔되지 않은 경우 자동 스캔
            if (_texturePaths.Count == 0)
            {
                ScanAll();
            }

            int totalAssets = _texturePaths.Count;

            if (totalAssets == 0)
            {
                EditorUtility.DisplayDialog(
                    "Texture Batch Importer",
                    "No textures found in the selected folders.",
                    "OK"
                );
                EditorGUILayout.EndHorizontal();
                return;
            }

            if (checkedCount == 0)
            {
                EditorUtility.DisplayDialog(
                    "Texture Batch Importer",
                    "No settings checked. Please check at least one setting to apply.",
                    "OK"
                );
                EditorGUILayout.EndHorizontal();
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "Texture Batch Importer",
                $"{totalAssets} textures will be modified with {checkedCount} settings.\nProceed?",
                "Apply",
                "Cancel"
            );

            if (confirm)
            {
                ApplySettings();
            }
        }

        GUI.enabled = true;

        DrawReferenceSlot();

        EditorGUILayout.EndHorizontal();
    }

    // Reference Texture 슬롯 (Apply 버튼 우측에 위치, 드롭 시 모든 설정 일괄 복사 + 전체 토글 ON)
    private void DrawReferenceSlot()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(220));
        GUILayout.FlexibleSpace();

        var prevRef = _referenceTexture;
        var newRef = (Texture2D)EditorGUILayout.ObjectField(
            new GUIContent("Ref Tex", "Drop a texture here to copy ALL its importer settings onto the tool (every toggle ON)."),
            _referenceTexture,
            typeof(Texture2D),
            false
        );

        if (newRef != prevRef)
        {
            _referenceTexture = newRef;
            if (newRef != null)
            {
                LoadFromReferenceTexture(newRef);
            }
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
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

    // Uint Slider 타입 토글 필드
    private void DrawToggleUint(string label, ref ToggleValue<uint> toggle, uint min, uint max)
    {
        EditorGUILayout.BeginHorizontal();
        toggle.enabled = GUILayout.Toggle(toggle.enabled, GUIContent.none, GUILayout.Width(15));

        GUI.enabled = toggle.enabled;
        toggle.value = (uint)EditorGUILayout.IntSlider(label, (int)toggle.value, (int)min, (int)max);
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

    // String 타입 토글 필드
    private void DrawToggleString(string label, ref ToggleValue<string> toggle)
    {
        EditorGUILayout.BeginHorizontal();
        toggle.enabled = GUILayout.Toggle(toggle.enabled, GUIContent.none, GUILayout.Width(15));

        GUI.enabled = toggle.enabled;
        toggle.value = EditorGUILayout.TextField(label, toggle.value);
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    // Float 타입 토글 필드
    private void DrawToggleFloat(string label, ref ToggleValue<float> toggle)
    {
        EditorGUILayout.BeginHorizontal();
        toggle.enabled = GUILayout.Toggle(toggle.enabled, GUIContent.none, GUILayout.Width(15));

        GUI.enabled = toggle.enabled;
        toggle.value = EditorGUILayout.FloatField(label, toggle.value);
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

    // Platform Settings 공통 그리기
    private void DrawPlatformSettings(PlatformSettings settings)
    {
        DrawToggleBool("Override", ref settings.overrideForPlatform);
        DrawPlatformMaxSize(settings);
        DrawToggleEnum("Format", ref settings.format);
        DrawToggleEnum("Compression", ref settings.compression);
        DrawToggleIntPopup("Compression Quality", ref settings.compressionQuality, CompressionQualityOptions, CompressionQualityLabels);
    }

    // Platform MaxSize 전용 (Use Default 체크박스 포함)
    private void DrawPlatformMaxSize(PlatformSettings settings)
    {
        EditorGUILayout.BeginHorizontal();
        settings.maxSize.enabled = GUILayout.Toggle(settings.maxSize.enabled, GUIContent.none, GUILayout.Width(15));

        if (settings.useDefaultMaxSize)
        {
            GUI.enabled = false;
            EditorGUILayout.TextField("Max Size", "(Each Texture's Default)");
            GUI.enabled = true;
        }
        else
        {
            GUI.enabled = settings.maxSize.enabled;
            int currentIndex = System.Array.IndexOf(MaxSizeOptions, settings.maxSize.value);
            if (currentIndex < 0) currentIndex = 6; // 기본값 2048
            int selectedIndex = EditorGUILayout.Popup("Max Size", currentIndex, MaxSizeLabels);
            settings.maxSize.value = MaxSizeOptions[selectedIndex];
            GUI.enabled = true;
        }

        GUI.enabled = settings.maxSize.enabled;
        settings.useDefaultMaxSize = GUILayout.Toggle(settings.useDefaultMaxSize, "Use Default", GUILayout.Width(90));
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
        if (currentIndex < 0) currentIndex = 1; // 기본값 Normal(50)
        int selectedIndex = EditorGUILayout.Popup(label, currentIndex, labels);
        toggle.value = options[selectedIndex];
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Private Methods

    // 대상 폴더 + 개별 텍스처에서 텍스처 파일 스캔
    private void ScanAll()
    {
        _texturePaths.Clear();

        string[] extensions = { "*.png", "*.jpg", "*.jpeg", "*.tga", "*.psd", "*.bmp", "*.gif", "*.tif", "*.tiff", "*.exr", "*.hdr" };

        // 폴더 스캔
        foreach (var folder in _targetFolders)
        {
            if (folder == null) continue;

            string folderPath = AssetDatabase.GetAssetPath(folder);
            if (!Directory.Exists(folderPath)) continue;

            foreach (string ext in extensions)
            {
                string[] files = Directory.GetFiles(folderPath, ext, SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string assetPath = file.Replace("\\", "/");
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer != null && !_texturePaths.Contains(assetPath))
                    {
                        _texturePaths.Add(assetPath);
                    }
                }
            }
        }

        // 개별 텍스처 추가
        foreach (var tex in _targetTextures)
        {
            if (tex == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(assetPath)) continue;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && !_texturePaths.Contains(assetPath))
            {
                _texturePaths.Add(assetPath);
            }
        }

        int folderCount = _targetFolders.FindAll(f => f != null).Count;
        int textureCount = _targetTextures.FindAll(t => t != null).Count;
        Debug.Log($"[TextureBatchImporter] Scanned {_texturePaths.Count} textures ({folderCount} folders, {textureCount} individual)");
    }

    // 체크된 설정을 모든 텍스처에 일괄 적용
    private void ApplySettings()
    {
        int total = _texturePaths.Count;
        int processed = 0;
        bool cancelled = false;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < total; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                    "Texture Batch Importer",
                    $"Applying to textures... ({i + 1}/{total})",
                    (float)i / total))
                {
                    cancelled = true;
                    break;
                }

                var importer = AssetImporter.GetAtPath(_texturePaths[i]) as TextureImporter;
                if (importer == null) continue;

                ApplyToTexture(importer);
                processed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        // 텍스처 리임포트
        int reimportCount = cancelled ? processed : total;
        for (int i = 0; i < reimportCount; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar(
                "Texture Batch Importer",
                $"Reimporting... ({i + 1}/{reimportCount})",
                (float)i / reimportCount))
            {
                break;
            }

            AssetDatabase.ImportAsset(_texturePaths[i], ImportAssetOptions.ForceUpdate);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();

        string result = cancelled
            ? $"Cancelled. Applied to {processed}/{total} textures."
            : $"Completed. Applied to {processed} textures.";
        Debug.Log($"[TextureBatchImporter] {result}");
        EditorUtility.DisplayDialog("Texture Batch Importer", result, "OK");
    }

    // 개별 텍스처에 체크된 설정 적용
    private void ApplyToTexture(TextureImporter importer)
    {
        // General
        if (_textureType.enabled) importer.textureType = _textureType.value;
        if (_textureShape.enabled) importer.textureShape = _textureShape.value;
        if (_sRGB.enabled) importer.sRGBTexture = _sRGB.value;
        if (_alphaSource.enabled) importer.alphaSource = _alphaSource.value;
        if (_alphaIsTransparency.enabled) importer.alphaIsTransparency = _alphaIsTransparency.value;
        if (_readWrite.enabled) importer.isReadable = _readWrite.value;
        if (_generateMipmaps.enabled) importer.mipmapEnabled = _generateMipmaps.value;
        if (_wrapMode.enabled) importer.wrapMode = _wrapMode.value;
        if (_filterMode.enabled) importer.filterMode = _filterMode.value;
        if (_anisoLevel.enabled) importer.anisoLevel = _anisoLevel.value;

        // Sprite
        if (_spriteMode.enabled) importer.spriteImportMode = _spriteMode.value;
        if (_packingTag.enabled) importer.spritePackingTag = _packingTag.value;
        if (_pixelsPerUnit.enabled) importer.spritePixelsPerUnit = _pixelsPerUnit.value;
        if (_meshType.enabled || _generatePhysicsShape.enabled || _extrudeEdges.enabled)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (_meshType.enabled) settings.spriteMeshType = _meshType.value;
            if (_generatePhysicsShape.enabled) settings.spriteGenerateFallbackPhysicsShape = _generatePhysicsShape.value;
            if (_extrudeEdges.enabled) settings.spriteExtrude = _extrudeEdges.value;
            importer.SetTextureSettings(settings);
        }

        // Default Platform
        if (_defaultMaxSize.enabled || _defaultFormat.enabled ||
            _defaultCompression.enabled || _defaultCompressionQuality.enabled)
        {
            if (_defaultMaxSize.enabled) importer.maxTextureSize = _defaultMaxSize.value;
            if (_defaultFormat.enabled)
            {
                var defaultSettings = importer.GetDefaultPlatformTextureSettings();
                defaultSettings.format = _defaultFormat.value;
                importer.SetPlatformTextureSettings(defaultSettings);
            }
            if (_defaultCompression.enabled) importer.textureCompression = _defaultCompression.value;
            if (_defaultCompressionQuality.enabled) importer.compressionQuality = _defaultCompressionQuality.value;
        }

        // Android / iOS
        ApplyPlatformSettings(importer, "Android", _androidSettings);
        ApplyPlatformSettings(importer, "iPhone", _iosSettings);
    }

    // 참조 이미지의 TextureImporter 설정을 도구 UI 값에 일괄 복사 + 모든 토글 ON
    private void LoadFromReferenceTexture(Texture2D refTex)
    {
        if (refTex == null) return;

        string path = AssetDatabase.GetAssetPath(refTex);
        if (string.IsNullOrEmpty(path))
        {
            UnityEngine.Debug.LogWarning("[TextureBatchImporter] Reference texture has no asset path");
            return;
        }

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            UnityEngine.Debug.LogWarning($"[TextureBatchImporter] Reference texture is not imported via TextureImporter: {path}");
            return;
        }

        // General
        _textureType.enabled = true; _textureType.value = importer.textureType;
        _textureShape.enabled = true; _textureShape.value = importer.textureShape;
        _sRGB.enabled = true; _sRGB.value = importer.sRGBTexture;
        _alphaSource.enabled = true; _alphaSource.value = importer.alphaSource;
        _alphaIsTransparency.enabled = true; _alphaIsTransparency.value = importer.alphaIsTransparency;
        _readWrite.enabled = true; _readWrite.value = importer.isReadable;
        _generateMipmaps.enabled = true; _generateMipmaps.value = importer.mipmapEnabled;
        _wrapMode.enabled = true; _wrapMode.value = importer.wrapMode;
        _filterMode.enabled = true; _filterMode.value = importer.filterMode;
        _anisoLevel.enabled = true; _anisoLevel.value = Mathf.Clamp(importer.anisoLevel, 0, 16);

        // Sprite
        _spriteMode.enabled = true; _spriteMode.value = importer.spriteImportMode;
        _packingTag.enabled = true; _packingTag.value = importer.spritePackingTag ?? "";
        _pixelsPerUnit.enabled = true; _pixelsPerUnit.value = importer.spritePixelsPerUnit;

        var spriteSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(spriteSettings);
        _meshType.enabled = true; _meshType.value = spriteSettings.spriteMeshType;
        _generatePhysicsShape.enabled = true; _generatePhysicsShape.value = spriteSettings.spriteGenerateFallbackPhysicsShape;
        _extrudeEdges.enabled = true; _extrudeEdges.value = (uint)Mathf.Clamp((int)spriteSettings.spriteExtrude, 0, 32);

        // Default Platform (popup 옵션 외 값은 가장 가까운 값으로 스냅)
        _defaultMaxSize.enabled = true; _defaultMaxSize.value = SnapToClosestOption(importer.maxTextureSize, MaxSizeOptions);
        var defaultPlatform = importer.GetDefaultPlatformTextureSettings();
        _defaultFormat.enabled = true; _defaultFormat.value = defaultPlatform.format;
        _defaultCompression.enabled = true; _defaultCompression.value = importer.textureCompression;
        _defaultCompressionQuality.enabled = true; _defaultCompressionQuality.value = SnapToClosestOption(importer.compressionQuality, CompressionQualityOptions);

        // Android / iOS — Override=false면 false로 그대로 반영
        LoadPlatformFromImporter(importer, "Android", _androidSettings);
        LoadPlatformFromImporter(importer, "iPhone", _iosSettings);

        UnityEngine.Debug.Log($"[TextureBatchImporter] Loaded reference settings from: {path} (all toggles ON)");
        Repaint();
    }

    // 참조 이미지에서 특정 플랫폼 설정을 PlatformSettings에 매핑
    private void LoadPlatformFromImporter(TextureImporter importer, string platform, PlatformSettings settings)
    {
        var ps = importer.GetPlatformTextureSettings(platform);

        settings.overrideForPlatform.enabled = true;
        settings.overrideForPlatform.value = ps.overridden;

        settings.maxSize.enabled = true;
        settings.maxSize.value = SnapToClosestOption(ps.maxTextureSize, MaxSizeOptions);
        settings.useDefaultMaxSize = false; // 참조 이미지의 명시 값을 그대로 사용

        settings.format.enabled = true;
        settings.format.value = ps.format;

        settings.compression.enabled = true;
        settings.compression.value = ps.textureCompression;

        settings.compressionQuality.enabled = true;
        settings.compressionQuality.value = SnapToClosestOption(ps.compressionQuality, CompressionQualityOptions);
    }

    // 임의 값을 고정 선택지 중 가장 가까운 값으로 스냅 (popup이 표시할 수 있는 값으로 정규화)
    private static int SnapToClosestOption(int value, int[] options)
    {
        if (options == null || options.Length == 0) return value;
        int closest = options[0];
        int minDiff = Mathf.Abs(value - closest);
        for (int i = 1; i < options.Length; i++)
        {
            int diff = Mathf.Abs(value - options[i]);
            if (diff < minDiff)
            {
                minDiff = diff;
                closest = options[i];
            }
        }
        return closest;
    }

    // 플랫폼별 설정 적용
    private void ApplyPlatformSettings(TextureImporter importer, string platform, PlatformSettings settings)
    {
        bool hasAnyEnabled = settings.overrideForPlatform.enabled ||
                             settings.maxSize.enabled ||
                             settings.format.enabled ||
                             settings.compression.enabled ||
                             settings.compressionQuality.enabled;

        if (!hasAnyEnabled) return;

        var platformSettings = importer.GetPlatformTextureSettings(platform);

        if (settings.overrideForPlatform.enabled)
            platformSettings.overridden = settings.overrideForPlatform.value;

        if (settings.maxSize.enabled)
            platformSettings.maxTextureSize = settings.useDefaultMaxSize ? importer.maxTextureSize : settings.maxSize.value;

        if (settings.format.enabled)
            platformSettings.format = settings.format.value;

        if (settings.compression.enabled)
            platformSettings.textureCompression = settings.compression.value;

        if (settings.compressionQuality.enabled)
            platformSettings.compressionQuality = settings.compressionQuality.value;

        importer.SetPlatformTextureSettings(platformSettings);
    }

    #endregion

    #region EditorPrefs

    // EditorPrefs 저장 헬퍼
    private void SaveBool(string key, bool value) => EditorPrefs.SetBool(P + key, value);
    private void SaveInt(string key, int value) => EditorPrefs.SetInt(P + key, value);
    private void SaveFloat(string key, float value) => EditorPrefs.SetFloat(P + key, value);
    private void SaveString(string key, string value) => EditorPrefs.SetString(P + key, value ?? "");

    // EditorPrefs 로드 헬퍼
    private bool LoadBool(string key, bool def) => EditorPrefs.GetBool(P + key, def);
    private int LoadInt(string key, int def) => EditorPrefs.GetInt(P + key, def);
    private float LoadFloat(string key, float def) => EditorPrefs.GetFloat(P + key, def);
    private string LoadString(string key, string def) => EditorPrefs.GetString(P + key, def ?? "");

    // 모든 설정을 EditorPrefs에 저장
    private void SaveAllPrefs()
    {
        // Target Folders
        SaveInt("folderCount", _targetFolders.Count);
        for (int i = 0; i < _targetFolders.Count; i++)
        {
            SaveString($"folder.{i}", _targetFolders[i] != null
                ? AssetDatabase.GetAssetPath(_targetFolders[i])
                : "");
        }

        // General
        SaveBool("textureType.on", _textureType.enabled);
        SaveInt("textureType.v", (int)_textureType.value);
        SaveBool("textureShape.on", _textureShape.enabled);
        SaveInt("textureShape.v", (int)_textureShape.value);
        SaveBool("sRGB.on", _sRGB.enabled);
        SaveBool("sRGB.v", _sRGB.value);
        SaveBool("alphaSource.on", _alphaSource.enabled);
        SaveInt("alphaSource.v", (int)_alphaSource.value);
        SaveBool("alphaIsTransparency.on", _alphaIsTransparency.enabled);
        SaveBool("alphaIsTransparency.v", _alphaIsTransparency.value);
        SaveBool("readWrite.on", _readWrite.enabled);
        SaveBool("readWrite.v", _readWrite.value);
        SaveBool("generateMipmaps.on", _generateMipmaps.enabled);
        SaveBool("generateMipmaps.v", _generateMipmaps.value);
        SaveBool("wrapMode.on", _wrapMode.enabled);
        SaveInt("wrapMode.v", (int)_wrapMode.value);
        SaveBool("filterMode.on", _filterMode.enabled);
        SaveInt("filterMode.v", (int)_filterMode.value);
        SaveBool("anisoLevel.on", _anisoLevel.enabled);
        SaveInt("anisoLevel.v", _anisoLevel.value);

        // Sprite
        SaveBool("spriteMode.on", _spriteMode.enabled);
        SaveInt("spriteMode.v", (int)_spriteMode.value);
        SaveBool("packingTag.on", _packingTag.enabled);
        SaveString("packingTag.v", _packingTag.value);
        SaveBool("pixelsPerUnit.on", _pixelsPerUnit.enabled);
        SaveFloat("pixelsPerUnit.v", _pixelsPerUnit.value);
        SaveBool("meshType.on", _meshType.enabled);
        SaveInt("meshType.v", (int)_meshType.value);
        SaveBool("generatePhysicsShape.on", _generatePhysicsShape.enabled);
        SaveBool("generatePhysicsShape.v", _generatePhysicsShape.value);
        SaveBool("extrudeEdges.on", _extrudeEdges.enabled);
        SaveInt("extrudeEdges.v", (int)_extrudeEdges.value);

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
        SaveBool("fold.general", _foldGeneral);
        SaveBool("fold.sprite", _foldSprite);
        SaveBool("fold.default", _foldDefault);
        SaveBool("fold.android", _foldAndroid);
        SaveBool("fold.ios", _foldIOS);
    }

    // EditorPrefs에서 모든 설정 복원
    private void LoadAllPrefs()
    {
        // Target Folders
        int folderCount = LoadInt("folderCount", 0);
        _targetFolders.Clear();
        for (int i = 0; i < folderCount; i++)
        {
            string path = LoadString($"folder.{i}", "");
            _targetFolders.Add(!string.IsNullOrEmpty(path)
                ? AssetDatabase.LoadAssetAtPath<DefaultAsset>(path)
                : null);
        }

        // General
        _textureType.enabled = LoadBool("textureType.on", _textureType.enabled);
        _textureType.value = (TextureImporterType)LoadInt("textureType.v", (int)_textureType.value);
        _textureShape.enabled = LoadBool("textureShape.on", _textureShape.enabled);
        _textureShape.value = (TextureImporterShape)LoadInt("textureShape.v", (int)_textureShape.value);
        _sRGB.enabled = LoadBool("sRGB.on", _sRGB.enabled);
        _sRGB.value = LoadBool("sRGB.v", _sRGB.value);
        _alphaSource.enabled = LoadBool("alphaSource.on", _alphaSource.enabled);
        _alphaSource.value = (TextureImporterAlphaSource)LoadInt("alphaSource.v", (int)_alphaSource.value);
        _alphaIsTransparency.enabled = LoadBool("alphaIsTransparency.on", _alphaIsTransparency.enabled);
        _alphaIsTransparency.value = LoadBool("alphaIsTransparency.v", _alphaIsTransparency.value);
        _readWrite.enabled = LoadBool("readWrite.on", _readWrite.enabled);
        _readWrite.value = LoadBool("readWrite.v", _readWrite.value);
        _generateMipmaps.enabled = LoadBool("generateMipmaps.on", _generateMipmaps.enabled);
        _generateMipmaps.value = LoadBool("generateMipmaps.v", _generateMipmaps.value);
        _wrapMode.enabled = LoadBool("wrapMode.on", _wrapMode.enabled);
        _wrapMode.value = (TextureWrapMode)LoadInt("wrapMode.v", (int)_wrapMode.value);
        _filterMode.enabled = LoadBool("filterMode.on", _filterMode.enabled);
        _filterMode.value = (FilterMode)LoadInt("filterMode.v", (int)_filterMode.value);
        _anisoLevel.enabled = LoadBool("anisoLevel.on", _anisoLevel.enabled);
        _anisoLevel.value = LoadInt("anisoLevel.v", _anisoLevel.value);

        // Sprite
        _spriteMode.enabled = LoadBool("spriteMode.on", _spriteMode.enabled);
        _spriteMode.value = (SpriteImportMode)LoadInt("spriteMode.v", (int)_spriteMode.value);
        _packingTag.enabled = LoadBool("packingTag.on", _packingTag.enabled);
        _packingTag.value = LoadString("packingTag.v", _packingTag.value);
        _pixelsPerUnit.enabled = LoadBool("pixelsPerUnit.on", _pixelsPerUnit.enabled);
        _pixelsPerUnit.value = LoadFloat("pixelsPerUnit.v", _pixelsPerUnit.value);
        _meshType.enabled = LoadBool("meshType.on", _meshType.enabled);
        _meshType.value = (SpriteMeshType)LoadInt("meshType.v", (int)_meshType.value);
        _generatePhysicsShape.enabled = LoadBool("generatePhysicsShape.on", _generatePhysicsShape.enabled);
        _generatePhysicsShape.value = LoadBool("generatePhysicsShape.v", _generatePhysicsShape.value);
        _extrudeEdges.enabled = LoadBool("extrudeEdges.on", _extrudeEdges.enabled);
        _extrudeEdges.value = (uint)LoadInt("extrudeEdges.v", (int)_extrudeEdges.value);

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
        _foldGeneral = LoadBool("fold.general", _foldGeneral);
        _foldSprite = LoadBool("fold.sprite", _foldSprite);
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
        SaveBool($"{prefix}.useDefaultMaxSize", s.useDefaultMaxSize);
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
        s.useDefaultMaxSize = LoadBool($"{prefix}.useDefaultMaxSize", s.useDefaultMaxSize);
        s.format.enabled = LoadBool($"{prefix}.format.on", s.format.enabled);
        s.format.value = (TextureImporterFormat)LoadInt($"{prefix}.format.v", (int)s.format.value);
        s.compression.enabled = LoadBool($"{prefix}.compression.on", s.compression.enabled);
        s.compression.value = (TextureImporterCompression)LoadInt($"{prefix}.compression.v", (int)s.compression.value);
        s.compressionQuality.enabled = LoadBool($"{prefix}.compressionQuality.on", s.compressionQuality.enabled);
        s.compressionQuality.value = LoadInt($"{prefix}.compressionQuality.v", s.compressionQuality.value);
    }

    #endregion

    #region Utility Methods

    // 모든 토글을 켜거나 끄기
    private void SetAllToggles(bool enabled)
    {
        // General
        _textureType.enabled = enabled;
        _textureShape.enabled = enabled;
        _sRGB.enabled = enabled;
        _alphaSource.enabled = enabled;
        _alphaIsTransparency.enabled = enabled;
        _readWrite.enabled = enabled;
        _generateMipmaps.enabled = enabled;
        _wrapMode.enabled = enabled;
        _filterMode.enabled = enabled;
        _anisoLevel.enabled = enabled;

        // Sprite
        _spriteMode.enabled = enabled;
        _packingTag.enabled = enabled;
        _pixelsPerUnit.enabled = enabled;
        _meshType.enabled = enabled;
        _generatePhysicsShape.enabled = enabled;
        _extrudeEdges.enabled = enabled;

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

        // General (10)
        if (_textureType.enabled) count++;
        if (_textureShape.enabled) count++;
        if (_sRGB.enabled) count++;
        if (_alphaSource.enabled) count++;
        if (_alphaIsTransparency.enabled) count++;
        if (_readWrite.enabled) count++;
        if (_generateMipmaps.enabled) count++;
        if (_wrapMode.enabled) count++;
        if (_filterMode.enabled) count++;
        if (_anisoLevel.enabled) count++;

        // Sprite (6)
        if (_spriteMode.enabled) count++;
        if (_packingTag.enabled) count++;
        if (_pixelsPerUnit.enabled) count++;
        if (_meshType.enabled) count++;
        if (_generatePhysicsShape.enabled) count++;
        if (_extrudeEdges.enabled) count++;

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
