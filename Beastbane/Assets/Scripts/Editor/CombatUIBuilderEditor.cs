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
                    CleanUpOldCombatController();

                    Undo.RegisterFullObjectHierarchyUndo(builder.gameObject, "Build Combat UI");
                    builder.Build();

                    for (int i = 0; i < builder.transform.childCount; i++)
                        Undo.RegisterCreatedObjectUndo(builder.transform.GetChild(i).gameObject, "Build Combat UI");

                    BuildCombatController(builder);
                    BuildLocalComponents(builder);

                    Undo.CollapseUndoOperations(group);
                    EditorUtility.SetDirty(builder);
                }

                if (GUILayout.Button("Clear UI", GUILayout.Height(30)))
                {
                    Undo.SetCurrentGroupName("Clear Combat UI");
                    int group = Undo.GetCurrentGroup();

                    for (int i = builder.transform.childCount - 1; i >= 0; i--)
                        Undo.DestroyObjectImmediate(builder.transform.GetChild(i).gameObject);

                    CleanUpOldCombatController();
                    CleanUpByName("CombatLocal");

                    Undo.CollapseUndoOperations(group);
                    EditorUtility.SetDirty(builder);
                }
            }
        }

        private static void CleanUpOldCombatController()
        {
            CleanUpByName("CombatController");
        }

        private static void CleanUpByName(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);
        }

        /// <summary>
        /// CombatManager + NetworkIdentity at scene root so Mirror auto-spawns it.
        /// Must NOT be under any SceneSwitcher child that gets disabled.
        /// </summary>
        private static void BuildCombatController(CombatUIBuilder builder)
        {
            var db = builder.DB;

            var go = new GameObject("CombatController");
            Undo.RegisterCreatedObjectUndo(go, "Build Combat UI");

            go.AddComponent<NetworkIdentity>();

            var combatManager = go.AddComponent<CombatManager>();
            SetSerializedField(combatManager, "_db", db);
        }

        /// <summary>
        /// CombatPresenter + CardRewardUI as a sibling of CombatUIBuilder (under CombatScene).
        /// NOT under CombatUIBuilder so Clear()/Build() doesn't destroy them.
        /// </summary>
        private static void BuildLocalComponents(CombatUIBuilder builder)
        {
            var db = builder.DB;

            var existing = GameObject.Find("CombatLocal");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var go = new GameObject("CombatLocal");
            if (builder.transform.parent != null)
                go.transform.SetParent(builder.transform.parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Build Combat UI");

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
