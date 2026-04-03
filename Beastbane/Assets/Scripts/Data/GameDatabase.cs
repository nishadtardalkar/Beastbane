using UnityEngine;

namespace Beastbane.Data
{
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "Beastbane/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        public CardData[] cards = System.Array.Empty<CardData>();
        public HeroData[] heroes = System.Array.Empty<HeroData>();
        public EnemyData[] enemies = System.Array.Empty<EnemyData>();

        public int GetCardIndex(CardData card)
        {
            for (int i = 0; i < cards.Length; i++)
                if (cards[i] == card) return i;
            return -1;
        }

        public CardData GetCard(int index) =>
            index >= 0 && index < cards.Length ? cards[index] : null;

        public HeroData GetHero(int index) =>
            index >= 0 && index < heroes.Length ? heroes[index] : null;

        public EnemyData GetEnemy(int index) =>
            index >= 0 && index < enemies.Length ? enemies[index] : null;
    }
}
