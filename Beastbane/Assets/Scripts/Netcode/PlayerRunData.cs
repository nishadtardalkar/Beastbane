using System.Collections.Generic;
using Beastbane.Data;
using Mirror;
using UnityEngine;

namespace Beastbane.Netcode
{
    /// <summary>
    /// Per-player persistent state across combats.
    /// Spawned as a network object (one per player in the lobby).
    /// Tracks hero choice, HP, deck, and gold throughout the run.
    /// </summary>
    public class PlayerRunData : NetworkBehaviour
    {
        [Header("Database")]
        [SerializeField] private GameDatabase _db;

        [SyncVar] private int _heroIndex = -1;
        [SyncVar] private int _currentHP;
        [SyncVar] private int _maxHP;
        [SyncVar] private int _energy;
        [SyncVar] private int _gold;

        public readonly SyncList<int> deck = new();

        public int HeroIndex => _heroIndex;
        public int CurrentHP => _currentHP;
        public int MaxHP => _maxHP;
        public int Energy => _energy;
        public int Gold => _gold;
        public bool HeroSelected => _heroIndex >= 0;

        public GameDatabase DB => _db;

        [Server]
        public void InitFromHero(int heroIndex)
        {
            if (_db == null)
            {
                Debug.LogError("PlayerRunData: GameDatabase not assigned.");
                return;
            }

            var hero = _db.GetHero(heroIndex);
            if (hero == null)
            {
                Debug.LogError($"PlayerRunData: Hero index {heroIndex} not found.");
                return;
            }

            _heroIndex = heroIndex;
            _maxHP = hero.maxHP;
            _currentHP = hero.maxHP;
            _energy = hero.startEnergy;
            _gold = 0;

            deck.Clear();
            foreach (var card in hero.startingDeck)
            {
                int idx = _db.GetCardIndex(card);
                if (idx >= 0)
                    deck.Add(idx);
                else
                    Debug.LogWarning($"PlayerRunData: Card '{card.cardName}' not found in database.");
            }

            Debug.Log($"PlayerRunData: Initialized hero '{hero.heroName}' with {deck.Count} cards, {_maxHP} HP.");
        }

        [Command(requiresAuthority = false)]
        public void CmdSelectHero(int heroIndex, NetworkConnectionToClient sender = null)
        {
            if (sender == null) return;
            if (_heroIndex >= 0)
            {
                Debug.LogWarning("PlayerRunData: Hero already selected.");
                return;
            }
            InitFromHero(heroIndex);
        }

        [Server]
        public void AddCardToDeck(int cardIndex)
        {
            deck.Add(cardIndex);
        }

        [Server]
        public void ApplyDamage(int amount)
        {
            _currentHP = Mathf.Max(0, _currentHP - amount);
        }

        [Server]
        public void Heal(int amount)
        {
            _currentHP = Mathf.Min(_maxHP, _currentHP + amount);
        }

        [Server]
        public void AddGold(int amount)
        {
            _gold += amount;
        }

        public List<int> GetDeckCopy()
        {
            var copy = new List<int>(deck.Count);
            foreach (int cardIdx in deck)
                copy.Add(cardIdx);
            return copy;
        }
    }
}
