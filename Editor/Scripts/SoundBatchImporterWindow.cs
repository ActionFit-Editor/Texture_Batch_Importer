#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class SoundBatchImporterWindow : EditorWindow
{
    private struct ToggleValue<T>
    {
        public bool enabled;
        public T value;

        public ToggleValue(T defaultValue)
        {
            enabled = false;
            value = defaultValue;
        }
    }

    private class PlatformSettings
    {
        public ToggleValue<bool> overrideForPlatform;
        public ToggleValue<AudioClipLoadType> loadType;
        public ToggleValue<AudioCompressionFormat> compressionFormat;
        public ToggleValue<float> quality;
        public ToggleValue<AudioSampleRateSetting> sampleRateSetting;
        public ToggleValue<uint> sampleRateOverride;
        public ToggleValue<bool> preloadAudioData;

        public PlatformSettings()
        {
            overrideForPlatform = new ToggleValue<bool>(false);
            loadType = new ToggleValue<AudioClipLoadType>(AudioClipLoadType.CompressedInMemory);
            compressionFormat = new ToggleValue<AudioCompressionFormat>(AudioCompressionFormat.Vorbis);
            quality = new ToggleValue<float>(0.5f);
            sampleRateSetting = new ToggleValue<AudioSampleRateSetting>(AudioSampleRateSetting.OptimizeSampleRate);
            sampleRateOverride = new ToggleValue<uint>(22050);
            preloadAudioData = new ToggleValue<bool>(true);
        }
    }

    private readonly List<DefaultAsset> _targetFolders = new();
    private readonly List<AudioClip> _targetClips = new();
    private readonly List<string> _audioPaths = new();
    private AudioClip _referenceClip;
    private Vector2 _scrollPosition;

    private ToggleValue<bool> _forceToMono;
    private ToggleValue<bool> _ambisonic;
    private ToggleValue<bool> _loadInBackground;

    private ToggleValue<AudioClipLoadType> _defaultLoadType;
    private ToggleValue<AudioCompressionFormat> _defaultCompressionFormat;
    private ToggleValue<float> _defaultQuality;
    private ToggleValue<AudioSampleRateSetting> _defaultSampleRateSetting;
    private ToggleValue<uint> _defaultSampleRateOverride;
    private ToggleValue<bool> _defaultPreloadAudioData;

    private PlatformSettings _androidSettings;
    private PlatformSettings _iosSettings;

    private bool _foldGeneral = true;
    private bool _foldDefault = true;
    private bool _foldAndroid = false;
    private bool _foldIOS = false;

    private const string P = "SoundBatchImporter.";

    private int CheckedSettingsCount => CountCheckedSettings();

    [MenuItem("Tools/ActionFit/Sound Batch Importer", false, 21)]
    public static void ShowWindow()
    {
        var window = GetWindow<SoundBatchImporterWindow>("Sound Batch Importer");
        window.minSize = new Vector2(420, 500);
        window.Show();
    }

    private void OnEnable()
    {
        InitializeDefaults();
        LoadAllPrefs();
    }

    private void OnDisable()
    {
        SaveAllPrefs();
    }

    private void InitializeDefaults()
    {
        _forceToMono = new ToggleValue<bool>(true);
        _ambisonic = new ToggleValue<bool>(false);
        _loadInBackground = new ToggleValue<bool>(false);

        _defaultLoadType = new ToggleValue<AudioClipLoadType>(AudioClipLoadType.CompressedInMemory);
        _defaultCompressionFormat = new ToggleValue<AudioCompressionFormat>(AudioCompressionFormat.Vorbis);
        _defaultQuality = new ToggleValue<float>(0.5f);
        _defaultSampleRateSetting = new ToggleValue<AudioSampleRateSetting>(AudioSampleRateSetting.OptimizeSampleRate);
        _defaultSampleRateOverride = new ToggleValue<uint>(22050);
        _defaultPreloadAudioData = new ToggleValue<bool>(true);

        _androidSettings = new PlatformSettings();
        _iosSettings = new PlatformSettings();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        DrawFolderSelection();
        EditorGUILayout.Space(5);

        DrawClipSelection();
        EditorGUILayout.Space(5);

        DrawSelectButtons();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        DrawGeneralSettings();
        DrawDefaultSampleSettings();
        DrawAndroidSettings();
        DrawIOSSettings();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(5);
        DrawApplyButton();
        EditorGUILayout.Space(5);
    }

    private void DrawFolderSelection()
    {
        EditorGUILayout.LabelField("Target Folders", EditorStyles.boldLabel);
        DrawFolderDropArea();

        bool needsRescan = false;
        bool showDialog = false;

        for (int i = 0; i < _targetFolders.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            var newFolder = (DefaultAsset)EditorGUILayout.ObjectField(_targetFolders[i], typeof(DefaultAsset), false);
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
            _audioPaths.Clear();
        }

        if (!needsRescan) return;

        ScanAll();
        if (showDialog && _audioPaths.Count > 0)
        {
            int folderCount = _targetFolders.FindAll(f => f != null).Count;
            EditorUtility.DisplayDialog("Sound Batch Importer", $"{_audioPaths.Count} audio clips found across {folderCount} folder(s).", "OK");
        }
    }

    private void DrawFolderDropArea()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0, 35, GUILayout.ExpandWidth(true));
        var style = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
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
                if (!Directory.Exists(path) || _targetFolders.Contains(folder)) continue;

                _targetFolders.Add(folder);
                addedCount++;
            }

            if (addedCount > 0)
            {
                ScanAll();
                int folderCount = _targetFolders.FindAll(f => f != null).Count;
                EditorUtility.DisplayDialog(
                    "Sound Batch Importer",
                    $"{_audioPaths.Count} audio clips found across {folderCount} folder(s).\n({addedCount} folder(s) added)",
                    "OK"
                );
            }

            evt.Use();
        }
    }

    private void DrawClipSelection()
    {
        EditorGUILayout.LabelField("Individual Audio Clips", EditorStyles.boldLabel);
        DrawClipDropArea();

        bool needsRescan = false;

        for (int i = 0; i < _targetClips.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            var newClip = (AudioClip)EditorGUILayout.ObjectField(_targetClips[i], typeof(AudioClip), false);
            if (newClip != _targetClips[i])
            {
                _targetClips[i] = newClip;
                needsRescan = true;
            }

            if (GUILayout.Button("X", GUILayout.Width(22)))
            {
                _targetClips.RemoveAt(i);
                needsRescan = true;
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (_targetClips.Count > 0 && GUILayout.Button("Remove All Audio Clips"))
        {
            _targetClips.Clear();
            needsRescan = true;
        }

        if (needsRescan) ScanAll();
    }

    private void DrawClipDropArea()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0, 35, GUILayout.ExpandWidth(true));
        var style = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
        GUI.Box(dropArea, "Drag & Drop Audio Clips Here", style);

        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition)) return;

        if (evt.type == EventType.DragUpdated)
        {
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is AudioClip)
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
                if (obj is not AudioClip clip) continue;
                if (_targetClips.Contains(clip)) continue;

                _targetClips.Add(clip);
                addedCount++;
            }

            if (addedCount > 0)
            {
                ScanAll();
                EditorUtility.DisplayDialog("Sound Batch Importer", $"{addedCount} audio clip(s) added. Total: {_audioPaths.Count} clips.", "OK");
            }

            evt.Use();
        }
    }

    private void DrawSelectButtons()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select All")) SetAllToggles(true);
        if (GUILayout.Button("Deselect All")) SetAllToggles(false);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
    }

    private void DrawGeneralSettings()
    {
        _foldGeneral = EditorGUILayout.Foldout(_foldGeneral, "General Settings", true, EditorStyles.foldoutHeader);
        if (!_foldGeneral) return;

        EditorGUI.indentLevel++;
        DrawToggleBool("Force To Mono", ref _forceToMono);
        DrawToggleBool("Ambisonic", ref _ambisonic);
        DrawToggleBool("Load In Background", ref _loadInBackground);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
    }

    private void DrawDefaultSampleSettings()
    {
        _foldDefault = EditorGUILayout.Foldout(_foldDefault, "Default Sample Settings", true, EditorStyles.foldoutHeader);
        if (!_foldDefault) return;

        EditorGUI.indentLevel++;
        DrawToggleEnum("Load Type", ref _defaultLoadType);
        DrawToggleEnum("Compression Format", ref _defaultCompressionFormat);
        DrawToggleQuality("Quality", ref _defaultQuality);
        DrawToggleEnum("Sample Rate Setting", ref _defaultSampleRateSetting);
        DrawToggleSampleRate("Sample Rate Override", ref _defaultSampleRateOverride);
        DrawToggleBool("Preload Audio Data", ref _defaultPreloadAudioData);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
    }

    private void DrawAndroidSettings()
    {
        _foldAndroid = EditorGUILayout.Foldout(_foldAndroid, "Android Sample Override", true, EditorStyles.foldoutHeader);
        if (!_foldAndroid) return;

        EditorGUI.indentLevel++;
        DrawPlatformSettings(_androidSettings);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
    }

    private void DrawIOSSettings()
    {
        _foldIOS = EditorGUILayout.Foldout(_foldIOS, "iOS Sample Override", true, EditorStyles.foldoutHeader);
        if (!_foldIOS) return;

        EditorGUI.indentLevel++;
        DrawPlatformSettings(_iosSettings);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
    }

    private void DrawPlatformSettings(PlatformSettings settings)
    {
        DrawToggleBool("Override", ref settings.overrideForPlatform);
        DrawToggleEnum("Load Type", ref settings.loadType);
        DrawToggleEnum("Compression Format", ref settings.compressionFormat);
        DrawToggleQuality("Quality", ref settings.quality);
        DrawToggleEnum("Sample Rate Setting", ref settings.sampleRateSetting);
        DrawToggleSampleRate("Sample Rate Override", ref settings.sampleRateOverride);
        DrawToggleBool("Preload Audio Data", ref settings.preloadAudioData);
    }

    private void DrawApplyButton()
    {
        int checkedCount = CheckedSettingsCount;

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = _targetFolders.Exists(f => f != null) || _targetClips.Exists(c => c != null);
        if (GUILayout.Button("Apply to All Audio Clips", GUILayout.Height(35)))
        {
            if (_audioPaths.Count == 0) ScanAll();

            int totalAssets = _audioPaths.Count;
            if (totalAssets == 0)
            {
                EditorUtility.DisplayDialog("Sound Batch Importer", "No audio clips found in the selected folders.", "OK");
                EditorGUILayout.EndHorizontal();
                return;
            }

            if (checkedCount == 0)
            {
                EditorUtility.DisplayDialog("Sound Batch Importer", "No settings checked. Please check at least one setting to apply.", "OK");
                EditorGUILayout.EndHorizontal();
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "Sound Batch Importer",
                $"{totalAssets} audio clips will be modified with {checkedCount} settings.\nProceed?",
                "Apply",
                "Cancel"
            );

            if (confirm) ApplySettings();
        }
        GUI.enabled = true;

        DrawReferenceSlot();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawReferenceSlot()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(220));
        GUILayout.FlexibleSpace();

        var prevRef = _referenceClip;
        var newRef = (AudioClip)EditorGUILayout.ObjectField(
            new GUIContent("Ref Clip", "Drop an AudioClip here to copy ALL its importer settings onto the tool (every toggle ON)."),
            _referenceClip,
            typeof(AudioClip),
            false
        );

        if (newRef != prevRef)
        {
            _referenceClip = newRef;
            if (newRef != null) LoadFromReferenceClip(newRef);
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
    }

    private void DrawToggleBool(string label, ref ToggleValue<bool> toggle)
    {
        EditorGUILayout.BeginHorizontal();
        toggle.enabled = GUILayout.Toggle(toggle.enabled, GUIContent.none, GUILayout.Width(15));
        GUI.enabled = toggle.enabled;
        toggle.value = EditorGUILayout.Toggle(label, toggle.value);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToggleEnum<T>(string label, ref ToggleValue<T> toggle) where T : System.Enum
    {
        EditorGUILayout.BeginHorizontal();
        toggle.enabled = GUILayout.Toggle(toggle.enabled, GUIContent.none, GUILayout.Width(15));
        GUI.enabled = toggle.enabled;
        toggle.value = (T)(object)EditorGUILayout.EnumPopup(label, toggle.value);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToggleQuality(string label, ref ToggleValue<float> toggle)
    {
        EditorGUILayout.BeginHorizontal();
        toggle.enabled = GUILayout.Toggle(toggle.enabled, GUIContent.none, GUILayout.Width(15));
        GUI.enabled = toggle.enabled;
        toggle.value = EditorGUILayout.Slider(label, toggle.value, 0f, 1f);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToggleSampleRate(string label, ref ToggleValue<uint> toggle)
    {
        EditorGUILayout.BeginHorizontal();
        toggle.enabled = GUILayout.Toggle(toggle.enabled, GUIContent.none, GUILayout.Width(15));
        GUI.enabled = toggle.enabled;
        int value = Mathf.Clamp((int)toggle.value, 8000, 192000);
        value = EditorGUILayout.IntField(label, value);
        toggle.value = (uint)Mathf.Clamp(value, 8000, 192000);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private void ScanAll()
    {
        _audioPaths.Clear();

        string[] extensions = { "*.wav", "*.mp3", "*.ogg", "*.aif", "*.aiff", "*.xm", "*.mod", "*.it", "*.s3m" };

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
                    if (AssetImporter.GetAtPath(assetPath) is AudioImporter && !_audioPaths.Contains(assetPath))
                    {
                        _audioPaths.Add(assetPath);
                    }
                }
            }
        }

        foreach (var clip in _targetClips)
        {
            if (clip == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(assetPath)) continue;

            if (AssetImporter.GetAtPath(assetPath) is AudioImporter && !_audioPaths.Contains(assetPath))
            {
                _audioPaths.Add(assetPath);
            }
        }

        int folderCount = _targetFolders.FindAll(f => f != null).Count;
        int clipCount = _targetClips.FindAll(c => c != null).Count;
        Debug.Log($"[SoundBatchImporter] Scanned {_audioPaths.Count} audio clips ({folderCount} folders, {clipCount} individual)");
    }

    private void ApplySettings()
    {
        int total = _audioPaths.Count;
        int processed = 0;
        bool cancelled = false;

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < total; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Sound Batch Importer", $"Applying to audio clips... ({i + 1}/{total})", (float)i / total))
                {
                    cancelled = true;
                    break;
                }

                if (AssetImporter.GetAtPath(_audioPaths[i]) is not AudioImporter importer) continue;

                ApplyToAudio(importer);
                processed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        int reimportCount = cancelled ? processed : total;
        for (int i = 0; i < reimportCount; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Sound Batch Importer", $"Reimporting... ({i + 1}/{reimportCount})", (float)i / reimportCount))
            {
                break;
            }

            AssetDatabase.ImportAsset(_audioPaths[i], ImportAssetOptions.ForceUpdate);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();

        string result = cancelled ? $"Cancelled. Applied to {processed}/{total} audio clips." : $"Completed. Applied to {processed} audio clips.";
        Debug.Log($"[SoundBatchImporter] {result}");
        EditorUtility.DisplayDialog("Sound Batch Importer", result, "OK");
    }

    private void ApplyToAudio(AudioImporter importer)
    {
        if (_forceToMono.enabled) importer.forceToMono = _forceToMono.value;
        if (_ambisonic.enabled) importer.ambisonic = _ambisonic.value;
        if (_loadInBackground.enabled) importer.loadInBackground = _loadInBackground.value;

        if (_defaultLoadType.enabled || _defaultCompressionFormat.enabled || _defaultQuality.enabled ||
            _defaultSampleRateSetting.enabled || _defaultSampleRateOverride.enabled || _defaultPreloadAudioData.enabled)
        {
            var settings = importer.defaultSampleSettings;
            ApplySampleSettings(ref settings,
                _defaultLoadType,
                _defaultCompressionFormat,
                _defaultQuality,
                _defaultSampleRateSetting,
                _defaultSampleRateOverride,
                _defaultPreloadAudioData);
            importer.defaultSampleSettings = settings;
        }

        ApplyPlatformSettings(importer, BuildTargetGroup.Android, _androidSettings);
        ApplyPlatformSettings(importer, BuildTargetGroup.iOS, _iosSettings);
    }

    private void ApplyPlatformSettings(AudioImporter importer, BuildTargetGroup platform, PlatformSettings settings)
    {
        bool hasAnyEnabled = settings.overrideForPlatform.enabled ||
                             settings.loadType.enabled ||
                             settings.compressionFormat.enabled ||
                             settings.quality.enabled ||
                             settings.sampleRateSetting.enabled ||
                             settings.sampleRateOverride.enabled ||
                             settings.preloadAudioData.enabled;

        if (!hasAnyEnabled) return;

        if (settings.overrideForPlatform.enabled && !settings.overrideForPlatform.value)
        {
            importer.ClearSampleSettingOverride(platform);
            return;
        }

        var sampleSettings = importer.ContainsSampleSettingsOverride(platform)
            ? importer.GetOverrideSampleSettings(platform)
            : importer.defaultSampleSettings;

        ApplySampleSettings(ref sampleSettings,
            settings.loadType,
            settings.compressionFormat,
            settings.quality,
            settings.sampleRateSetting,
            settings.sampleRateOverride,
            settings.preloadAudioData);

        importer.SetOverrideSampleSettings(platform, sampleSettings);
    }

    private static void ApplySampleSettings(
        ref AudioImporterSampleSettings settings,
        ToggleValue<AudioClipLoadType> loadType,
        ToggleValue<AudioCompressionFormat> compressionFormat,
        ToggleValue<float> quality,
        ToggleValue<AudioSampleRateSetting> sampleRateSetting,
        ToggleValue<uint> sampleRateOverride,
        ToggleValue<bool> preloadAudioData)
    {
        if (loadType.enabled) settings.loadType = loadType.value;
        if (compressionFormat.enabled) settings.compressionFormat = compressionFormat.value;
        if (quality.enabled) settings.quality = Mathf.Clamp01(quality.value);
        if (sampleRateSetting.enabled) settings.sampleRateSetting = sampleRateSetting.value;
        if (sampleRateOverride.enabled) settings.sampleRateOverride = sampleRateOverride.value;
        if (preloadAudioData.enabled) settings.preloadAudioData = preloadAudioData.value;
    }

    private void LoadFromReferenceClip(AudioClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("[SoundBatchImporter] Reference clip has no asset path");
            return;
        }

        if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
        {
            Debug.LogWarning($"[SoundBatchImporter] Reference clip is not imported via AudioImporter: {path}");
            return;
        }

        _forceToMono.enabled = true; _forceToMono.value = importer.forceToMono;
        _ambisonic.enabled = true; _ambisonic.value = importer.ambisonic;
        _loadInBackground.enabled = true; _loadInBackground.value = importer.loadInBackground;

        LoadSampleSettings(importer.defaultSampleSettings,
            ref _defaultLoadType,
            ref _defaultCompressionFormat,
            ref _defaultQuality,
            ref _defaultSampleRateSetting,
            ref _defaultSampleRateOverride,
            ref _defaultPreloadAudioData);

        LoadPlatformFromImporter(importer, BuildTargetGroup.Android, _androidSettings);
        LoadPlatformFromImporter(importer, BuildTargetGroup.iOS, _iosSettings);

        Debug.Log($"[SoundBatchImporter] Loaded reference settings from: {path} (all toggles ON)");
        Repaint();
    }

    private static void LoadSampleSettings(
        AudioImporterSampleSettings settings,
        ref ToggleValue<AudioClipLoadType> loadType,
        ref ToggleValue<AudioCompressionFormat> compressionFormat,
        ref ToggleValue<float> quality,
        ref ToggleValue<AudioSampleRateSetting> sampleRateSetting,
        ref ToggleValue<uint> sampleRateOverride,
        ref ToggleValue<bool> preloadAudioData)
    {
        loadType.enabled = true; loadType.value = settings.loadType;
        compressionFormat.enabled = true; compressionFormat.value = settings.compressionFormat;
        quality.enabled = true; quality.value = Mathf.Clamp01(settings.quality);
        sampleRateSetting.enabled = true; sampleRateSetting.value = settings.sampleRateSetting;
        sampleRateOverride.enabled = true; sampleRateOverride.value = settings.sampleRateOverride;
        preloadAudioData.enabled = true; preloadAudioData.value = settings.preloadAudioData;
    }

    private static void LoadPlatformFromImporter(AudioImporter importer, BuildTargetGroup platform, PlatformSettings settings)
    {
        bool hasOverride = importer.ContainsSampleSettingsOverride(platform);
        settings.overrideForPlatform.enabled = true;
        settings.overrideForPlatform.value = hasOverride;

        var sampleSettings = hasOverride ? importer.GetOverrideSampleSettings(platform) : importer.defaultSampleSettings;
        LoadSampleSettings(sampleSettings,
            ref settings.loadType,
            ref settings.compressionFormat,
            ref settings.quality,
            ref settings.sampleRateSetting,
            ref settings.sampleRateOverride,
            ref settings.preloadAudioData);
    }

    private int CountCheckedSettings()
    {
        int count = 0;
        if (_forceToMono.enabled) count++;
        if (_ambisonic.enabled) count++;
        if (_loadInBackground.enabled) count++;

        if (_defaultLoadType.enabled) count++;
        if (_defaultCompressionFormat.enabled) count++;
        if (_defaultQuality.enabled) count++;
        if (_defaultSampleRateSetting.enabled) count++;
        if (_defaultSampleRateOverride.enabled) count++;
        if (_defaultPreloadAudioData.enabled) count++;

        count += CountPlatformSettings(_androidSettings);
        count += CountPlatformSettings(_iosSettings);
        return count;
    }

    private static int CountPlatformSettings(PlatformSettings settings)
    {
        int count = 0;
        if (settings.overrideForPlatform.enabled) count++;
        if (settings.loadType.enabled) count++;
        if (settings.compressionFormat.enabled) count++;
        if (settings.quality.enabled) count++;
        if (settings.sampleRateSetting.enabled) count++;
        if (settings.sampleRateOverride.enabled) count++;
        if (settings.preloadAudioData.enabled) count++;
        return count;
    }

    private void SetAllToggles(bool enabled)
    {
        _forceToMono.enabled = enabled;
        _ambisonic.enabled = enabled;
        _loadInBackground.enabled = enabled;

        _defaultLoadType.enabled = enabled;
        _defaultCompressionFormat.enabled = enabled;
        _defaultQuality.enabled = enabled;
        _defaultSampleRateSetting.enabled = enabled;
        _defaultSampleRateOverride.enabled = enabled;
        _defaultPreloadAudioData.enabled = enabled;

        SetPlatformToggles(_androidSettings, enabled);
        SetPlatformToggles(_iosSettings, enabled);
    }

    private static void SetPlatformToggles(PlatformSettings settings, bool enabled)
    {
        settings.overrideForPlatform.enabled = enabled;
        settings.loadType.enabled = enabled;
        settings.compressionFormat.enabled = enabled;
        settings.quality.enabled = enabled;
        settings.sampleRateSetting.enabled = enabled;
        settings.sampleRateOverride.enabled = enabled;
        settings.preloadAudioData.enabled = enabled;
    }

    private void SaveAllPrefs()
    {
        SaveToggleBool("forceToMono", _forceToMono);
        SaveToggleBool("ambisonic", _ambisonic);
        SaveToggleBool("loadInBackground", _loadInBackground);

        SaveToggleEnum("defaultLoadType", _defaultLoadType);
        SaveToggleEnum("defaultCompressionFormat", _defaultCompressionFormat);
        SaveToggleFloat("defaultQuality", _defaultQuality);
        SaveToggleEnum("defaultSampleRateSetting", _defaultSampleRateSetting);
        SaveToggleUint("defaultSampleRateOverride", _defaultSampleRateOverride);
        SaveToggleBool("defaultPreloadAudioData", _defaultPreloadAudioData);

        SavePlatformPrefs("android", _androidSettings);
        SavePlatformPrefs("ios", _iosSettings);
    }

    private void LoadAllPrefs()
    {
        _forceToMono = LoadToggleBool("forceToMono", _forceToMono);
        _ambisonic = LoadToggleBool("ambisonic", _ambisonic);
        _loadInBackground = LoadToggleBool("loadInBackground", _loadInBackground);

        _defaultLoadType = LoadToggleEnum("defaultLoadType", _defaultLoadType);
        _defaultCompressionFormat = LoadToggleEnum("defaultCompressionFormat", _defaultCompressionFormat);
        _defaultQuality = LoadToggleFloat("defaultQuality", _defaultQuality);
        _defaultSampleRateSetting = LoadToggleEnum("defaultSampleRateSetting", _defaultSampleRateSetting);
        _defaultSampleRateOverride = LoadToggleUint("defaultSampleRateOverride", _defaultSampleRateOverride);
        _defaultPreloadAudioData = LoadToggleBool("defaultPreloadAudioData", _defaultPreloadAudioData);

        LoadPlatformPrefs("android", _androidSettings);
        LoadPlatformPrefs("ios", _iosSettings);
    }

    private static void SavePlatformPrefs(string key, PlatformSettings settings)
    {
        SaveToggleBool($"{key}.override", settings.overrideForPlatform);
        SaveToggleEnum($"{key}.loadType", settings.loadType);
        SaveToggleEnum($"{key}.compressionFormat", settings.compressionFormat);
        SaveToggleFloat($"{key}.quality", settings.quality);
        SaveToggleEnum($"{key}.sampleRateSetting", settings.sampleRateSetting);
        SaveToggleUint($"{key}.sampleRateOverride", settings.sampleRateOverride);
        SaveToggleBool($"{key}.preloadAudioData", settings.preloadAudioData);
    }

    private static void LoadPlatformPrefs(string key, PlatformSettings settings)
    {
        settings.overrideForPlatform = LoadToggleBool($"{key}.override", settings.overrideForPlatform);
        settings.loadType = LoadToggleEnum($"{key}.loadType", settings.loadType);
        settings.compressionFormat = LoadToggleEnum($"{key}.compressionFormat", settings.compressionFormat);
        settings.quality = LoadToggleFloat($"{key}.quality", settings.quality);
        settings.sampleRateSetting = LoadToggleEnum($"{key}.sampleRateSetting", settings.sampleRateSetting);
        settings.sampleRateOverride = LoadToggleUint($"{key}.sampleRateOverride", settings.sampleRateOverride);
        settings.preloadAudioData = LoadToggleBool($"{key}.preloadAudioData", settings.preloadAudioData);
    }

    private static void SaveToggleBool(string key, ToggleValue<bool> value)
    {
        EditorPrefs.SetBool($"{P}{key}.enabled", value.enabled);
        EditorPrefs.SetBool($"{P}{key}.value", value.value);
    }

    private static ToggleValue<bool> LoadToggleBool(string key, ToggleValue<bool> fallback)
    {
        fallback.enabled = EditorPrefs.GetBool($"{P}{key}.enabled", fallback.enabled);
        fallback.value = EditorPrefs.GetBool($"{P}{key}.value", fallback.value);
        return fallback;
    }

    private static void SaveToggleFloat(string key, ToggleValue<float> value)
    {
        EditorPrefs.SetBool($"{P}{key}.enabled", value.enabled);
        EditorPrefs.SetFloat($"{P}{key}.value", value.value);
    }

    private static ToggleValue<float> LoadToggleFloat(string key, ToggleValue<float> fallback)
    {
        fallback.enabled = EditorPrefs.GetBool($"{P}{key}.enabled", fallback.enabled);
        fallback.value = EditorPrefs.GetFloat($"{P}{key}.value", fallback.value);
        return fallback;
    }

    private static void SaveToggleUint(string key, ToggleValue<uint> value)
    {
        EditorPrefs.SetBool($"{P}{key}.enabled", value.enabled);
        EditorPrefs.SetInt($"{P}{key}.value", (int)value.value);
    }

    private static ToggleValue<uint> LoadToggleUint(string key, ToggleValue<uint> fallback)
    {
        fallback.enabled = EditorPrefs.GetBool($"{P}{key}.enabled", fallback.enabled);
        fallback.value = (uint)Mathf.Max(0, EditorPrefs.GetInt($"{P}{key}.value", (int)fallback.value));
        return fallback;
    }

    private static void SaveToggleEnum<T>(string key, ToggleValue<T> value) where T : System.Enum
    {
        EditorPrefs.SetBool($"{P}{key}.enabled", value.enabled);
        EditorPrefs.SetInt($"{P}{key}.value", System.Convert.ToInt32(value.value));
    }

    private static ToggleValue<T> LoadToggleEnum<T>(string key, ToggleValue<T> fallback) where T : System.Enum
    {
        fallback.enabled = EditorPrefs.GetBool($"{P}{key}.enabled", fallback.enabled);
        int value = EditorPrefs.GetInt($"{P}{key}.value", System.Convert.ToInt32(fallback.value));
        if (System.Enum.IsDefined(typeof(T), value)) fallback.value = (T)System.Enum.ToObject(typeof(T), value);
        return fallback;
    }
}

#endif
