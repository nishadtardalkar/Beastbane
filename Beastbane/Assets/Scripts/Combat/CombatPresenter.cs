using System.Collections.Generic;
using Beastbane.Data;
using Beastbane.UI;
using Mirror;
using UnityEngine;

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
            if (_db == null) return;

            var cards = new List<CardDisplayData>();
            foreach (int cardIdx in _combat.hand)
            {
                var card = _db.GetCard(cardIdx);
                if (card == null) continue;
                cards.Add(new CardDisplayData
                {
                    Name = card.cardName,
                    Cost = card.manaCost,
                    Type = card.cardType,
                    Art = card.sprite
                });
            }

            _ui.UpdateHand(cards);
        }

        private void HandleInput()
        {
            if (_combat.Phase != CombatPhase.PlayerTurn) return;
            if (!IsFighter()) return;
            if (!Input.GetMouseButtonDown(0)) return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector2 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
            var hit = Physics2D.OverlapPoint(worldPos);
            if (hit == null) return;

            // End turn button
            if (hit.transform.parent != null && hit.transform.parent.name == "EndTurnButton")
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

        private bool IsFighter()
        {
            return _combat != null && _combat.IsMyFight;
        }
    }
}
