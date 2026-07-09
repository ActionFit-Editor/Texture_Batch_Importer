#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TextureBatchImporterPackageMenu
{
    private const string MenuRoot = "Tools/Package/Texture Batch Importer/";
    private const string ReadmePath = "Packages/com.actionfit.texturebatchimporter/README.md";
    private const int ReadmePriority = 901;

    [MenuItem(MenuRoot + "README", false, ReadmePriority)]
    private static void OpenReadme()
    {
        var readme = AssetDatabase.LoadAssetAtPath<TextAsset>(ReadmePath);
        if (readme == null)
        {
            EditorUtility.DisplayDialog("Package README", $"README was not found.\n{ReadmePath}", "OK");
            return;
        }

        Selection.activeObject = readme;
        AssetDatabase.OpenAsset(readme);
    }
}
#endif
