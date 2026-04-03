using UnityEngine;

namespace Beastbane.Data
{
    public enum CardType
    {
        Attack,
        Skill
    }

    [CreateAssetMenu(fileName = "NewCard", menuName = "Beastbane/Card Data")]
    public class CardData : ScriptableObject
    {
        public string cardName = "Card";
        [TextArea] public string description;
        public int manaCost = 1;
        public CardType cardType = CardType.Attack;
        public int damage;
        public int block;
        public Sprite sprite;
    }
}
