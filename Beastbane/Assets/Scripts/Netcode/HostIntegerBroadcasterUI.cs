using Mirror;
using UnityEngine;

namespace Beastbane.Netcode
{
    /// <summary>
    /// Optional helper for UI button/input wiring in editor.
    /// </summary>
    public sealed class HostIntegerBroadcasterUI : MonoBehaviour
    {
        [SerializeField] private HostIntegerBroadcaster broadcaster;
        [SerializeField] private int valueToSend = 1;
        [SerializeField] private bool incrementAfterSend = true;
        [SerializeField] private KeyCode debugSendKey = KeyCode.F8;

        private void Update()
        {
            if (!Input.GetKeyDown(debugSendKey)) return;
            SendConfiguredValue();
        }

        public void SetValueFromString(string text)
        {
            if (!int.TryParse(text, out var parsed))
            {
                Debug.LogWarning($"HostIntegerBroadcasterUI: invalid integer '{text}'.");
                return;
            }

            valueToSend = parsed;
        }

        public void SendConfiguredValue()
        {
            var target = ResolveBroadcaster();
            if (target == null)
            {
                Debug.LogWarning("HostIntegerBroadcasterUI: HostIntegerBroadcaster not found.");
                return;
            }

            if (!NetworkServer.active)
            {
                Debug.LogWarning("HostIntegerBroadcasterUI: only host/server can broadcast.");
                return;
            }

            target.BroadcastFromHost(valueToSend);

            if (incrementAfterSend)
            {
                valueToSend++;
            }
        }

        private HostIntegerBroadcaster ResolveBroadcaster()
        {
            if (broadcaster != null) return broadcaster;
            broadcaster = FindFirstObjectByType<HostIntegerBroadcaster>();
            return broadcaster;
        }
    }
}
