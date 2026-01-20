//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEngine;
//using UnityEngine.UI;

//[CustomEditor(typeof(ContainerSlots), editorForChildClasses: false)]
//[CanEditMultipleObjects] // enable multi-object editing (no params)
//public class ContainerSlotsUIEditor : Editor
//{
//    public override void OnInspectorGUI()   
//    {
//        DrawDefaultInspector();

//        if (GUILayout.Button("Generate / Refresh Inventory"))
//        {
//            foreach (Object obj in targets)
//            {
//                var inv = obj as ContainerSlots;
//                if (inv == null || inv.slotParent == null) continue;

//                // Record undo for hierarchy changes under slotParent
//                Undo.RegisterFullObjectHierarchyUndo(inv.slotParent.gameObject, "Generate Inventory");

//                inv.GenerateSlots();

//                // Mark dirty so changes persist
//                EditorUtility.SetDirty(inv.slotParent);
//                EditorUtility.SetDirty(inv);
//            }

//            // Repaint scene so UI updates immediately
//            SceneView.RepaintAll();
//        }
//    }
//}
//#endif
