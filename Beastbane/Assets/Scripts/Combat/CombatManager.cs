using System;
using System.Collections;
using System.Collections.Generic;
using Beastbane.Data;
using Beastbane.Netcode;
using Mirror;
using UnityEngine;

namespace Beastbane.Combat
{
    public enum CombatPhase
    {
        Idle,
        PlayerTurn,
        EnemyTurn,
        Reward,
        GameOver
    }

    /// <summary>
    /// Server-authoritative combat manager. Syncs all combat state to every client
    /// so spectators can watch. Only the fighting player can send Commands.
    /// </summary>
    public class CombatManager : NetworkBehaviour
    {
        [Header("Database")]
        [SerializeField] private GameDatabase _db;

        [Header("Combat Settings")]
        [SerializeField] private int _cardsPerDraw = 5;
        [SerializeField] private float _enemyTurnDelay = 1f;

        // ── Synced state (visible to all players) ───────────────────

        [SyncVar(hook = nameof(OnPhaseChanged))]
        private CombatPhase _phase = CombatPhase.Idle;

        [SyncVar] private int _activeConnectionId = -1;
        [SyncVar] private uint _activePlayerNetId;
        [SyncVar] private int _playerHP;
        [SyncVar] private int _playerMaxHP;
        [SyncVar] private int _playerBlock;
        [SyncVar] private int _playerEnergy;
        [SyncVar] private int _playerMaxEnergy;
        [SyncVar] private int _enemyHP;
        [SyncVar] private int _enemyMaxHP;
        [SyncVar] private int _enemyBlock;
        [SyncVar] private int _enemyIntentType;
        [SyncVar] private int _enemyIntentValue;
        [SyncVar] private int _enemyIndex = -1;
        [SyncVar] private int _heroIndex = -1;
        [SyncVar] private int _drawPileCount;

        public readonly SyncList<int> hand = new();
        public readonly SyncList<int> discardPile = new();
        public readonly SyncList<int> rewardCards = new();

        // ── Server-only state ───────────────────────────────────────

        private readonly List<int> _drawPile = new();
        private readonly List<int> _fullDeck = new();
        private int _enemyActionIndex;
        private System.Random _combatRng;

        // ── Public accessors ────────────────────────────────────────

        public CombatPhase Phase => _phase;
        public int ActiveConnectionId => _activeConnectionId;
        public uint ActivePlayerNetId => _activePlayerNetId;

        public bool IsMyFight =>
            NetworkClient.localPlayer != null && _activePlayerNetId == NetworkClient.localPlayer.netId;
        public int PlayerHP => _playerHP;
        public int PlayerMaxHP => _playerMaxHP;
        public int PlayerBlock => _playerBlock;
        public int PlayerEnergy => _playerEnergy;
        public int PlayerMaxEnergy => _playerMaxEnergy;
        public int EnemyHP => _enemyHP;
        public int EnemyMaxHP => _enemyMaxHP;
        public int EnemyBlock => _enemyBlock;
        public int EnemyIntentType => _enemyIntentType;
        public int EnemyIntentValue => _enemyIntentValue;
        public int EnemyIndex => _enemyIndex;
        public int HeroIndex => _heroIndex;
        public int DrawPileCount => _drawPileCount;
        public GameDatabase DB => _db;

        public event Action PhaseChanged;
        public event Action CombatEnded;

        // ── Server: Init ────────────────────────────────────────────

        [Server]
        public void InitCombat(int connectionId, int heroIndex, List<int> deck,
            int currentHP, int maxHP, int energy, int enemyIndex)
        {
            _activeConnectionId = connectionId;
            _heroIndex = heroIndex;
            _enemyIndex = enemyIndex;

            if (NetworkServer.connections.TryGetValue(connectionId, out var conn) && conn?.identity != null)
                _activePlayerNetId = conn.identity.netId;
            else
                _activePlayerNetId = 0;

            _playerMaxHP = maxHP;
            _playerHP = currentHP;
            _playerBlock = 0;
            _playerMaxEnergy = energy;
            _playerEnergy = energy;

            var enemy = _db.GetEnemy(enemyIndex);
            _enemyMaxHP = enemy != null ? enemy.maxHP : 50;
            _enemyHP = _enemyMaxHP;
            _enemyBlock = 0;
            _enemyActionIndex = 0;

            _combatRng = new System.Random(Environment.TickCount);

            _fullDeck.Clear();
            _fullDeck.AddRange(deck);

            hand.Clear();
            discardPile.Clear();
            rewardCards.Clear();

            ShuffleDeckIntoDrawPile();
            StartPlayerTurn();
        }

        // ── Server: Turn Flow ───────────────────────────────────────

        [Server]
        private void StartPlayerTurn()
        {
            _playerBlock = 0;
            _playerEnergy = _playerMaxEnergy;
            DrawCards(_cardsPerDraw);
            PickEnemyIntent();
            _phase = CombatPhase.PlayerTurn;
        }

        [Server]
        private void StartEnemyTurn()
        {
            _phase = CombatPhase.EnemyTurn;
            StartCoroutine(EnemyTurnCoroutine());
        }

        [Server]
        private IEnumerator EnemyTurnCoroutine()
        {
            yield return new WaitForSeconds(_enemyTurnDelay);

            ResolveEnemyAction();

            if (_playerHP <= 0)
            {
                EndCombat(false);
                yield break;
            }

            _enemyBlock = 0;
            StartPlayerTurn();
        }

        [Server]
        private void EndCombat(bool won)
        {
            hand.Clear();

            if (won)
            {
                GenerateRewardCards();
                _phase = CombatPhase.Reward;
            }
            else
            {
                _phase = CombatPhase.GameOver;
            }
        }

        // ── Server: Card Operations ─────────────────────────────────

        [Server]
        private void DrawCards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_drawPile.Count == 0)
                {
                    if (discardPile.Count == 0) break;
                    ReshuffleDiscard();
                }

                if (_drawPile.Count == 0) break;

                int cardIdx = _drawPile[_drawPile.Count - 1];
                _drawPile.RemoveAt(_drawPile.Count - 1);
                hand.Add(cardIdx);
                _drawPileCount = _drawPile.Count;
            }
        }

        [Server]
        private void ResolveCard(int handIndex)
        {
            if (handIndex < 0 || handIndex >= hand.Count) return;

            int cardIdx = hand[handIndex];
            var card = _db.GetCard(cardIdx);
            if (card == null) return;

            if (card.manaCost > _playerEnergy) return;

            _playerEnergy -= card.manaCost;

            if (card.damage > 0)
            {
                int dmg = card.damage;
                if (_enemyBlock >= dmg)
                {
                    _enemyBlock -= dmg;
                }
                else
                {
                    dmg -= _enemyBlock;
                    _enemyBlock = 0;
                    _enemyHP = Mathf.Max(0, _enemyHP - dmg);
                }
            }

            if (card.block > 0)
                _playerBlock += card.block;

            hand.RemoveAt(handIndex);
            discardPile.Add(cardIdx);

            if (_enemyHP <= 0)
                EndCombat(true);
        }

        [Server]
        private void DiscardHand()
        {
            foreach (int cardIdx in hand)
                discardPile.Add(cardIdx);
            hand.Clear();
        }

        [Server]
        private void ShuffleDeckIntoDrawPile()
        {
            _drawPile.Clear();
            _drawPile.AddRange(_fullDeck);
            Shuffle(_drawPile);
            _drawPileCount = _drawPile.Count;
        }

        [Server]
        private void ReshuffleDiscard()
        {
            foreach (int cardIdx in discardPile)
                _drawPile.Add(cardIdx);
            discardPile.Clear();
            Shuffle(_drawPile);
            _drawPileCount = _drawPile.Count;
        }

        // ── Server: Enemy AI ────────────────────────────────────────

        [Server]
        private void PickEnemyIntent()
        {
            var enemy = _db.GetEnemy(_enemyIndex);
            if (enemy == null || enemy.actions.Count == 0)
            {
                _enemyIntentType = (int)IntentType.Attack;
                _enemyIntentValue = 6;
                return;
            }

            var action = enemy.actions[_enemyActionIndex % enemy.actions.Count];
            _enemyIntentType = (int)action.intentType;
            _enemyIntentValue = action.intentType == IntentType.Attack ? action.damage : action.block;
        }

        [Server]
        private void ResolveEnemyAction()
        {
            var enemy = _db.GetEnemy(_enemyIndex);
            if (enemy == null || enemy.actions.Count == 0) return;

            var action = enemy.actions[_enemyActionIndex % enemy.actions.Count];
            _enemyActionIndex++;

            if (action.intentType == IntentType.Attack)
            {
                int dmg = action.damage;
                if (_playerBlock >= dmg)
                {
                    _playerBlock -= dmg;
                }
                else
                {
                    dmg -= _playerBlock;
                    _playerBlock = 0;
                    _playerHP = Mathf.Max(0, _playerHP - dmg);
                }
            }

            if (action.intentType == IntentType.Defend)
                _enemyBlock += action.block;
        }

        [Server]
        private void GenerateRewardCards()
        {
            rewardCards.Clear();
            if (_db == null || _db.cards.Length == 0) return;

            var used = new HashSet<int>();
            int attempts = 0;
            while (rewardCards.Count < 3 && attempts < 30)
            {
                int idx = _combatRng.Next(0, _db.cards.Length);
                if (used.Add(idx))
                    rewardCards.Add(idx);
                attempts++;
            }
        }

        // ── Commands (fighter-only) ─────────────────────────────────

        [Command(requiresAuthority = false)]
        public void CmdPlayCard(int handIndex, NetworkConnectionToClient sender = null)
        {
            if (sender == null || sender.connectionId != _activeConnectionId) return;
            if (_phase != CombatPhase.PlayerTurn) return;
            ResolveCard(handIndex);
        }

        [Command(requiresAuthority = false)]
        public void CmdEndPlayerTurn(NetworkConnectionToClient sender = null)
        {
            if (sender == null || sender.connectionId != _activeConnectionId) return;
            if (_phase != CombatPhase.PlayerTurn) return;
            DiscardHand();
            StartEnemyTurn();
        }

        [Command(requiresAuthority = false)]
        public void CmdPickReward(int rewardIndex, NetworkConnectionToClient sender = null)
        {
            if (sender == null || sender.connectionId != _activeConnectionId) return;
            if (_phase != CombatPhase.Reward) return;
            if (rewardIndex < 0 || rewardIndex >= rewardCards.Count) return;

            int cardIdx = rewardCards[rewardIndex];

            var runData = FindPlayerRunData(_activeConnectionId);
            if (runData != null)
            {
                runData.AddCardToDeck(cardIdx);
                runData.ApplyDamage(_playerMaxHP - _playerHP);
            }

            rewardCards.Clear();
            _phase = CombatPhase.Idle;
            CombatEnded?.Invoke();
        }

        [Command(requiresAuthority = false)]
        public void CmdSkipReward(NetworkConnectionToClient sender = null)
        {
            if (sender == null || sender.connectionId != _activeConnectionId) return;
            if (_phase != CombatPhase.Reward) return;

            var runData = FindPlayerRunData(_activeConnectionId);
            if (runData != null)
                runData.ApplyDamage(_playerMaxHP - _playerHP);

            rewardCards.Clear();
            _phase = CombatPhase.Idle;
            CombatEnded?.Invoke();
        }

        // ── Helpers ─────────────────────────────────────────────────

        private void OnPhaseChanged(CombatPhase oldPhase, CombatPhase newPhase)
        {
            PhaseChanged?.Invoke();
        }

        private static PlayerRunData FindPlayerRunData(int connectionId)
        {
            foreach (var prd in FindObjectsByType<PlayerRunData>(FindObjectsSortMode.None))
            {
                if (prd.connectionToClient != null && prd.connectionToClient.connectionId == connectionId)
                    return prd;
            }
            return null;
        }

        private void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _combatRng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
