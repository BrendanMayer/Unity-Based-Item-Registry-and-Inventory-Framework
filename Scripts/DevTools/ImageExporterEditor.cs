using UnityEditor;
using UnityEngine;
using System.IO;

[CustomEditor(typeof(ImageExporter))]
public class ImageExporterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);
        var exporter = (ImageExporter)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Image", GUILayout.Height(28)))
            {
                // Save into projectFolder/fileName
                exporter.Export();

                // Ping the asset if it exists
                var assetPath = $"{exporter.projectFolder}/{exporter.fileName}.png";
                var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (obj) EditorGUIUtility.PingObject(obj);
            }

            if (GUILayout.Button("Save As...", GUILayout.Height(28)))
            {
                string defaultDir = Directory.Exists(exporter.projectFolder)
                                  ? exporter.projectFolder
                                  : Application.dataPath;

                string path = EditorUtility.SaveFilePanel(
                    "Save PNG",
                    defaultDir,
                    exporter.fileName,
                    "png"
                );

                if (!string.IsNullOrEmpty(path))
                {
                    exporter.ExportToFullPath(path);

                    // If saved inside project, convert importer to Sprite (2D and UI)
                    var projectRoot = Path.GetFullPath(Application.dataPath + "/..") + Path.DirectorySeparatorChar;
                    var full = Path.GetFullPath(path);

                    if (full.StartsWith(projectRoot))
                    {
                        // Turn absolute path into "Assets/..."
                        string rel = full.Substring(projectRoot.Length).Replace('\\', '/');
                        if (!rel.StartsWith("Assets/"))
                            rel = "Assets/" + rel; // safety, though rel should already start with Assets/

                        ImageExporter.MakeSpriteIfInProject(rel, exporter.spritePixelsPerUnit, exporter.spriteFilterMode);
                        var obj = AssetDatabase.LoadAssetAtPath<Object>(rel);
                        if (obj) EditorGUIUtility.PingObject(obj);
                    }
                    else
                    {
                        Debug.Log("[ImageExporter] Saved outside the project. Import settings (Sprite) cannot be applied automatically.");
                    }
                }
            }
        }
    }
}
