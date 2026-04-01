using Beastbane.Netcode;
using UnityEngine;

namespace Beastbane.Map
{
    /// <summary>
    /// Attached to each map node GameObject by MapVisualizer.
    /// Handles click via OnMouseDown — requires a Collider2D on the same object.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class MapNodeClickHandler : MonoBehaviour
    {
        public string NodeId { get; set; }
        public bool EndTurnAfterMove { get; set; } = true;

        private PlayerMapState _playerMapState;
        private TurnState _turnState;

        private void OnMouseDown()
        {
            Debug.Log($"MapNodeClickHandler: clicked node '{NodeId}'");

            if (_turnState == null)
                _turnState = FindAnyObjectByType<TurnState>();

            if (_turnState == null)
            {
                Debug.LogWarning("MapNodeClickHandler: TurnState not found.");
                return;
            }

            if (!_turnState.IsMyTurn)
            {
                Debug.Log($"MapNodeClickHandler: not my turn (active conn={_turnState.ActiveConnectionId}).");
                return;
            }

            if (_playerMapState == null)
                _playerMapState = FindAnyObjectByType<PlayerMapState>();
            if (_playerMapState == null) return;

            _playerMapState.CmdRequestMove(NodeId);

            if (EndTurnAfterMove)
                _turnState.CmdEndTurn();
        }
    }
}
