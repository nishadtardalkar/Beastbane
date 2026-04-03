using System;
using System.Collections.Generic;
using UnityEngine;

namespace Beastbane.Data
{
    public enum IntentType
    {
        Attack,
        Defend
    }

    [Serializable]
    public struct EnemyAction
    {
        public IntentType intentType;
        public int damage;
        public int block;
    }

    [CreateAssetMenu(fileName = "NewEnemy", menuName = "Beastbane/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        public string enemyName = "Enemy";
        public Sprite sprite;
        public int maxHP = 50;
        public List<EnemyAction> actions = new();
    }
}
