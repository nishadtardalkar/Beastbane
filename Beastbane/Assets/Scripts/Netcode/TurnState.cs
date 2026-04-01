using System;
using Mirror;
using UnityEngine;

namespace Beastbane.Netcode
{
    /// <summary>
    /// Syncs whose turn is active among all players.
    /// Attach to a network-spawned object (e.g. the MapSpawner prefab).
    /// Cycles through connected players in connection order.
    /// </summary>
    public class TurnState : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnActivePlayerChanged))]
        private int _activeConnectionId = -1;

        [SyncVar]
        private uint _activePlayerNetId;

        private readonly SyncList<int> _turnOrder = new();

        /// <summary>Fires on all clients: (previousConnectionId, newConnectionId)</summary>
        public event Action<int, int> TurnChanged;

        public int ActiveConnectionId => _activeConnectionId;

        public bool IsMyTurn =>
            NetworkClient.localPlayer != null && _activePlayerNetId == NetworkClient.localPlayer.netId;

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.OnConnectedEvent += OnServerPlayerConnected;
            NetworkServer.OnDisconnectedEvent += OnServerPlayerDisconnected;
            BuildTurnOrder();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            NetworkServer.OnConnectedEvent -= OnServerPlayerConnected;
            NetworkServer.OnDisconnectedEvent -= OnServerPlayerDisconnected;
        }

        [Server]
        public void EndTurn()
        {
            if (_turnOrder.Count == 0) return;

            int currentIndex = _turnOrder.IndexOf(_activeConnectionId);
            int nextIndex = (currentIndex + 1) % _turnOrder.Count;
            SetActiveConnection(_turnOrder[nextIndex]);
        }

        [Command(requiresAuthority = false)]
        public void CmdEndTurn(NetworkConnectionToClient sender = null)
        {
            if (sender == null) return;
            if (sender.connectionId != _activeConnectionId)
            {
                Debug.LogWarning($"TurnState: conn {sender.connectionId} tried to end turn but it's conn {_activeConnectionId}'s turn.");
                return;
            }
            EndTurn();
        }

        [Server]
        private void SetActiveConnection(int connectionId)
        {
            int old = _activeConnectionId;
            _activeConnectionId = connectionId;

            if (connectionId >= 0 &&
                NetworkServer.connections.TryGetValue(connectionId, out var conn) &&
                conn?.identity != null)
            {
                _activePlayerNetId = conn.identity.netId;
            }
            else
            {
                _activePlayerNetId = 0;
            }

            Debug.Log($"TurnState: turn passed from conn {old} to conn {_activeConnectionId}");
        }

        [Server]
        private void BuildTurnOrder()
        {
            _turnOrder.Clear();
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn != null)
                    _turnOrder.Add(conn.connectionId);
            }

            if (_turnOrder.Count > 0 && _activeConnectionId == -1)
                SetActiveConnection(_turnOrder[0]);
        }

        [Server]
        private void OnServerPlayerConnected(NetworkConnectionToClient conn)
        {
            if (!_turnOrder.Contains(conn.connectionId))
                _turnOrder.Add(conn.connectionId);

            if (_activeConnectionId == -1)
                SetActiveConnection(_turnOrder[0]);
        }

        [Server]
        private void OnServerPlayerDisconnected(NetworkConnectionToClient conn)
        {
            bool wasActive = conn.connectionId == _activeConnectionId;
            _turnOrder.Remove(conn.connectionId);

            if (wasActive && _turnOrder.Count > 0)
                SetActiveConnection(_turnOrder[0]);
            else if (_turnOrder.Count == 0)
                SetActiveConnection(-1);
        }

        private void OnActivePlayerChanged(int oldId, int newId)
        {
            TurnChanged?.Invoke(oldId, newId);
        }
    }
}
