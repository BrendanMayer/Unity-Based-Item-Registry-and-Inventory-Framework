using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ImageExporter : MonoBehaviour
{
    [Header("What to render")]
    public Camera sourceCamera;

    [Header("Output size (square)")]
    [Min(1)] public int size = 512;

    [Header("Default Project Save (if you click 'Save Image')")]
    // Project-relative folder (must start with "Assets/")
    public string projectFolder = "Assets/Resources/Exports";
    public string fileName = "export";

    [Header("Quality")]
    [Range(1, 8)] public int msaa = 4;

    [Header("Auto Sprite Import (when saved inside Assets)")]
    public bool autoMakeSprite2D = true;
    public float spritePixelsPerUnit = 100f;
    public FilterMode spriteFilterMode = FilterMode.Bilinear;

    /// <summary>
    /// Quick save that uses projectFolder + fileName.
    /// </summary>
    public void Export()
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(projectFolder) || !projectFolder.StartsWith("Assets"))
        {
            Debug.LogError("[ImageExporter] 'projectFolder' must be inside the project and start with 'Assets/'.");
            return;
        }
        Directory.CreateDirectory(projectFolder);
        string assetPath = $"{projectFolder}/{fileName}.png";
        string fullPath = Path.GetFullPath(assetPath);

        ExportToFullPath(fullPath);

        // If saved into project, optionally convert to Sprite
        if (autoMakeSprite2D)
        {
            MakeSpriteIfInProject(assetPath, spritePixelsPerUnit, spriteFilterMode);
        }
#else
        Debug.LogError("[ImageExporter] Export() uses editor paths. Use ExportToPersistentData() at runtime.");
#endif
    }

    /// <summary>
    /// Core export routine: writes a transparent PNG to a full filesystem path.
    /// </summary>
    public void ExportToFullPath(string fullPath)
    {
        if (!sourceCamera)
        {
            Debug.LogError("[ImageExporter] No camera assigned.");
            return;
        }

        // Remember camera state
        var prevTarget = sourceCamera.targetTexture;
        var prevFlags = sourceCamera.clearFlags;
        var prevBg = sourceCamera.backgroundColor;

        // (URP) temporarily disable post-processing (via reflection) to keep alpha intact
        object urpData = null;
        bool prevPP = false;
        int prevAA = 0;
        try
        {
            var uacdType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (uacdType != null)
            {
                urpData = sourceCamera.GetComponent(uacdType);
                if (urpData != null)
                {
                    var ppProp = uacdType.GetProperty("renderPostProcessing");
                    var aaProp = uacdType.GetProperty("antialiasing");
                    if (ppProp != null) { prevPP = (bool)ppProp.GetValue(urpData); ppProp.SetValue(urpData, false); }
                    if (aaProp != null) { prevAA = (int)aaProp.GetValue(urpData); /* leave AA as-is */ }
                }
            }
        }
        catch { /* ignore */ }

        // Force a transparent clear
        sourceCamera.clearFlags = CameraClearFlags.SolidColor;
        sourceCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);

        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = Mathf.Clamp(msaa, 1, 8),
            useMipMap = false,
            autoGenerateMips = false
        };

        var prevActive = RenderTexture.active;
        try
        {
            sourceCamera.targetTexture = rt;

            RenderTexture.active = rt;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));

            sourceCamera.Render();

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();

            var png = tex.EncodeToPNG();

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, png);

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            Debug.Log($"[ImageExporter] Saved PNG with alpha to: {fullPath}");
        }
        finally
        {
            RenderTexture.active = prevActive;
            sourceCamera.targetTexture = prevTarget;
            sourceCamera.clearFlags = prevFlags;
            sourceCamera.backgroundColor = prevBg;

            if (urpData != null)
            {
                try
                {
                    var t = urpData.GetType();
                    var ppProp = t.GetProperty("renderPostProcessing");
                    var aaProp = t.GetProperty("antialiasing");
                    if (ppProp != null) ppProp.SetValue(urpData, prevPP);
                    if (aaProp != null) aaProp.SetValue(urpData, prevAA);
                }
                catch { }
            }

#if UNITY_EDITOR
            if (rt) DestroyImmediate(rt);
#else
            if (rt) Destroy(rt);
#endif
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// If the fullPath is under the project, convert to "Assets/..." and set importer to Sprite.
    /// </summary>
    public static void MakeSpriteIfInProject(string assetPath, float ppu, FilterMode filter)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets"))
            return;

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true; // safe default
        importer.sRGBTexture = true;
        importer.filterMode = filter;
        importer.spritePixelsPerUnit = ppu;

        // Optional: no compression artifacts on UI
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        importer.SaveAndReimport();
        Debug.Log($"[ImageExporter] Set importer to Sprite (2D and UI): {assetPath}");
    }
#endif
}
