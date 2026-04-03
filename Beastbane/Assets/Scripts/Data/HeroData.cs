using System.Collections.Generic;
using UnityEngine;

namespace Beastbane.Data
{
    [CreateAssetMenu(fileName = "NewHero", menuName = "Beastbane/Hero Data")]
    public class HeroData : ScriptableObject
    {
        public string heroName = "Hero";
        [TextArea] public string description;
        public Sprite sprite;
        public int maxHP = 80;
        public int startEnergy = 3;
        public List<CardData> startingDeck = new();
    }
}
