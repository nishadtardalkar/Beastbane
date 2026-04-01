using System;
using UnityEngine;

namespace Beastbane.UI
{
    /// <summary>
    /// Attach to the topmost parent. Each child GameObject acts as a "scene".
    /// Only one scene is active at a time; the rest are disabled.
    /// </summary>
    public class SceneSwitcher : MonoBehaviour
    {
        [Tooltip("Index of the scene to show on start. -1 = keep current state.")]
        [SerializeField] private int _defaultSceneIndex = 0;

        /// <summary>Fires when scene changes: (previousIndex, newIndex)</summary>
        public event Action<int, int> SceneChanged;

        public int ActiveSceneIndex { get; private set; } = -1;
        public int SceneCount => transform.childCount;

        private void Awake()
        {
            if (_defaultSceneIndex >= 0)
                SwitchTo(_defaultSceneIndex);
        }

        /// <summary>Activate a scene by its sibling index.</summary>
        public void SwitchTo(int index)
        {
            if (index < 0 || index >= transform.childCount)
            {
                Debug.LogWarning($"SceneSwitcher: index {index} out of range (0–{transform.childCount - 1}).");
                return;
            }

            int prev = ActiveSceneIndex;
            for (int i = 0; i < transform.childCount; i++)
                transform.GetChild(i).gameObject.SetActive(i == index);

            ActiveSceneIndex = index;
            SceneChanged?.Invoke(prev, index);
        }

        /// <summary>Activate a scene by its GameObject name.</summary>
        public void SwitchTo(string sceneName)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                if (string.Equals(transform.GetChild(i).name, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    SwitchTo(i);
                    return;
                }
            }

            Debug.LogWarning($"SceneSwitcher: no child named '{sceneName}' found.");
        }

        /// <summary>
        /// Button-friendly overload — pass the child name as a string arg
        /// in the Button.onClick static parameters.
        /// </summary>
        public void SwitchToByName(string sceneName) => SwitchTo(sceneName);

        public GameObject GetScene(int index) =>
            index >= 0 && index < transform.childCount ? transform.GetChild(index).gameObject : null;

        public GameObject GetScene(string sceneName)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                if (string.Equals(transform.GetChild(i).name, sceneName, StringComparison.OrdinalIgnoreCase))
                    return transform.GetChild(i).gameObject;
            }
            return null;
        }
    }
}
