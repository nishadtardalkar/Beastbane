using UnityEditor;
using UnityEngine;
using Beastbane.UI;

namespace Beastbane.EditorScripts
{
    [CustomEditor(typeof(CombatUIBuilder))]
    public class CombatUIBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var builder = (CombatUIBuilder)target;

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Combat UI", GUILayout.Height(30)))
                {
                    Undo.SetCurrentGroupName("Build Combat UI");
                    int group = Undo.GetCurrentGroup();

                    builder.Clear();

                    Undo.RegisterFullObjectHierarchyUndo(builder.gameObject, "Build Combat UI");
                    builder.Build();

                    for (int i = 0; i < builder.transform.childCount; i++)
                        Undo.RegisterCreatedObjectUndo(builder.transform.GetChild(i).gameObject, "Build Combat UI");

                    Undo.CollapseUndoOperations(group);
                    EditorUtility.SetDirty(builder);
                }

                if (GUILayout.Button("Clear UI", GUILayout.Height(30)))
                {
                    Undo.SetCurrentGroupName("Clear Combat UI");
                    int group = Undo.GetCurrentGroup();

                    for (int i = builder.transform.childCount - 1; i >= 0; i--)
                        Undo.DestroyObjectImmediate(builder.transform.GetChild(i).gameObject);

                    Undo.CollapseUndoOperations(group);
                    EditorUtility.SetDirty(builder);
                }
            }
        }
    }
}
