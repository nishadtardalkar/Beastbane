using System.Collections.Generic;
using Beastbane.Data;
using Beastbane.UI;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beastbane.Combat
{
    /// <summary>
    /// Bridges CombatManager synced state to CombatUIBuilder visuals.
    /// All players see the UI; only the fighter can interact.
    /// </summary>
    public class CombatPresenter : MonoBehaviour
    {
        [SerializeField] private CombatUIBuilder _ui;
        [SerializeField] private GameDatabase _db;

        private CombatManager _combat;
        private CombatPhase _lastPhase = CombatPhase.Idle;
        private bool _built;
        private readonly List<int> _lastHandSnapshot = new();

        private void LateUpdate()
        {
            if (_combat == null)
            {
                _combat = FindAnyObjectByType<CombatManager>();
                if (_combat == null) return;
            }

            if (_combat.Phase == CombatPhase.Idle)
            {
                if (_built)
                {
                    _ui.Clear();
                    _built = false;
                    _lastPhase = CombatPhase.Idle;
                    _lastHandSnapshot.Clear();
                }
                return;
            }

            if (!_built)
            {
                _ui.Build();
                _built = true;
                SetupSprites();
            }

            RefreshUI();
            HandleInput();

            _lastPhase = _combat.Phase;
        }

        private void SetupSprites()
        {
            if (_db == null) return;

            var hero = _db.GetHero(_combat.HeroIndex);
            if (hero != null)
            {
                _ui.SetHeroSprite(hero.sprite);
                _ui.UpdatePlayerName(hero.heroName);
            }

            var enemy = _db.GetEnemy(_combat.EnemyIndex);
            if (enemy != null)
                _ui.SetVillainSprite(enemy.sprite);
        }

        private void RefreshUI()
        {
            _ui.UpdatePlayerHP(_combat.PlayerHP, _combat.PlayerMaxHP, _combat.PlayerBlock);
            _ui.UpdateEnemyHP(_combat.EnemyHP, _combat.EnemyMaxHP, _combat.EnemyBlock);
            _ui.UpdateEnergy(_combat.PlayerEnergy, _combat.PlayerMaxEnergy);
            _ui.UpdateIntent((IntentType)_combat.EnemyIntentType, _combat.EnemyIntentValue);
            _ui.UpdateDrawPile(_combat.DrawPileCount);
            _ui.UpdateDiscardPile(_combat.discardPile.Count);

            bool isFighter = IsFighter();
            _ui.SetInteractable(isFighter && _combat.Phase == CombatPhase.PlayerTurn);

            RebuildHand();
        }

        private void RebuildHand()
        {
            if (!HandChanged()) return;

            SnapshotHand();

            var cards = new List<CardDisplayData>();
            foreach (int cardIdx in _combat.hand)
            {
                var card = _db != null ? _db.GetCard(cardIdx) : null;
                if (card != null)
                {
                    cards.Add(new CardDisplayData
                    {
                        Name = card.cardName,
                        Cost = card.manaCost,
                        Type = card.cardType,
                        Art = card.sprite
                    });
                }
                else
                {
                    bool isStrike = cardIdx != CombatManager.FallbackDefend;
                    cards.Add(new CardDisplayData
                    {
                        Name = isStrike ? "Strike" : "Defend",
                        Cost = 1,
                        Type = isStrike ? CardType.Attack : CardType.Skill,
                        Art = null
                    });
                }
            }

            _ui.UpdateHand(cards);
        }

        private bool HandChanged()
        {
            if (_lastHandSnapshot.Count != _combat.hand.Count) return true;
            for (int i = 0; i < _lastHandSnapshot.Count; i++)
            {
                if (_lastHandSnapshot[i] != _combat.hand[i]) return true;
            }
            return false;
        }

        private void SnapshotHand()
        {
            _lastHandSnapshot.Clear();
            foreach (int idx in _combat.hand)
                _lastHandSnapshot.Add(idx);
        }

        private void HandleInput()
        {
            if (_combat.Phase != CombatPhase.PlayerTurn)
            {
                DebugThrottle($"[CombatPresenter] HandleInput blocked: Phase={_combat.Phase} (want PlayerTurn)");
                return;
            }

            if (!IsFighter())
            {
                DebugThrottle($"[CombatPresenter] HandleInput blocked: IsFighter=false  " +
                              $"localPlayer={(NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId.ToString() : "NULL")}  " +
                              $"activePlayerNetId={_combat.ActivePlayerNetId}");
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                DebugThrottle("[CombatPresenter] HandleInput blocked: Mouse.current is null");
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame) return;

            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[CombatPresenter] Camera.main is null!"); return; }

            Vector2 worldPos = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            var hit = Physics2D.OverlapPoint(worldPos);

            Debug.Log($"[CombatPresenter] Click at screen={mouse.position.ReadValue()} world={worldPos}  hit={hit?.gameObject.name ?? "NULL"}  slots={_ui.CardSlots?.Length ?? -1}");

            if (hit == null) return;

            // End turn button (collider is on the EndTurnButton object itself)
            if (hit.transform.name == "EndTurnButton")
            {
                _combat.CmdEndPlayerTurn();
                return;
            }

            // Card clicks
            if (_ui.CardSlots == null) return;
            for (int i = 0; i < _ui.CardSlots.Length; i++)
            {
                if (_ui.CardSlots[i] != null && hit.transform == _ui.CardSlots[i])
                {
                    _combat.CmdPlayCard(i);
                    return;
                }
            }
        }

        private int _debugThrottleFrame = -1;
        private void DebugThrottle(string msg)
        {
            if (Time.frameCount == _debugThrottleFrame) return;
            _debugThrottleFrame = Time.frameCount;
            Debug.Log(msg);
        }

        private bool IsFighter()
        {
            return _combat != null && _combat.IsMyFight;
        }
    }
}
