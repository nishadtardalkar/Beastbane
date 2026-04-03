using UnityEditor;
using UnityEngine;
using Beastbane.Combat;
using Beastbane.UI;
using Mirror;

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

                    BuildCombatController(builder);

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

        private static void BuildCombatController(CombatUIBuilder builder)
        {
            var db = builder.DB;

            var go = new GameObject("CombatController");
            go.transform.SetParent(builder.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "Build Combat UI");

            go.AddComponent<NetworkIdentity>();

            var combatManager = go.AddComponent<CombatManager>();
            SetSerializedField(combatManager, "_db", db);

            var presenter = go.AddComponent<CombatPresenter>();
            SetSerializedField(presenter, "_ui", builder);
            SetSerializedField(presenter, "_db", db);

            var rewardUI = go.AddComponent<CardRewardUI>();
            SetSerializedField(rewardUI, "_db", db);
        }

        private static void SetSerializedField(Object component, string fieldName, Object value)
        {
            if (value == null) return;
            var so = new SerializedObject(component);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
