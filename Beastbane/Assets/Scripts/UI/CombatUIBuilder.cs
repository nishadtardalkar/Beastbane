using System.Collections.Generic;
using Beastbane.Data;
using TMPro;
using UnityEngine;

namespace Beastbane.UI
{
    public struct CardDisplayData
    {
        public string Name;
        public int Cost;
        public CardType Type;
        public Sprite Art;
    }

    /// <summary>
    /// Generates a Slay-the-Spire-style combat scene using SpriteRenderers and
    /// world-space TextMeshPro under the attached GameObject. No Canvas required.
    /// Assumes an orthographic camera with size ~5 (visible area roughly 18x10 units).
    /// </summary>
    public class CombatUIBuilder : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private int _cardCount = 5;
        [SerializeField] private int _maxEnergy = 3;
        [SerializeField] private int _currentEnergy = 3;

        [Header("Sprites (assign in Inspector)")]
        [SerializeField] private Sprite _heroSprite;
        [SerializeField] private Sprite _villainSprite;
        [SerializeField] private Sprite _backgroundSprite;

        [Header("Combat Setup")]
        [SerializeField] private GameDatabase _db;

        public GameDatabase DB => _db;

        [HideInInspector] public SpriteRenderer BackgroundRenderer;
        [HideInInspector] public SpriteRenderer HeroRenderer;
        [HideInInspector] public SpriteRenderer VillainRenderer;

        [HideInInspector] public TextMeshPro PlayerNameText;
        [HideInInspector] public TextMeshPro PlayerHPBarText;
        [HideInInspector] public SpriteRenderer PlayerHPFill;
        [HideInInspector] public TextMeshPro GoldText;
        [HideInInspector] public TextMeshPro FloorText;

        [HideInInspector] public TextMeshPro EnemyHPBarText;
        [HideInInspector] public SpriteRenderer EnemyHPFill;
        [HideInInspector] public TextMeshPro EnemyIntentText;
        [HideInInspector] public SpriteRenderer EnemyIntentBG;

        [HideInInspector] public TextMeshPro EnergyText;
        [HideInInspector] public TextMeshPro EndTurnText;
        [HideInInspector] public TextMeshPro DrawPileText;
        [HideInInspector] public TextMeshPro DiscardPileText;

        [HideInInspector] public Transform CardHandParent;
        [HideInInspector] public Transform[] CardSlots;
        [HideInInspector] public TextMeshPro[] CardNameTexts;
        [HideInInspector] public TextMeshPro[] CardCostTexts;
        [HideInInspector] public SpriteRenderer[] CardArtRenderers;

        private static readonly Color DarkBg = new(0.12f, 0.1f, 0.14f, 0.85f);
        private static readonly Color CardBg = new(0.18f, 0.14f, 0.12f, 1f);
        private static readonly Color CardBorder = new(0.55f, 0.4f, 0.2f, 1f);
        private static readonly Color HPRed = new(0.8f, 0.15f, 0.15f, 1f);
        private static readonly Color HPGreen = new(0.2f, 0.75f, 0.2f, 1f);
        private static readonly Color EnergyOrange = new(0.9f, 0.55f, 0.1f, 1f);
        private static readonly Color GoldYellow = new(1f, 0.85f, 0.3f, 1f);
        private static readonly Color SlotGray = new(0.35f, 0.35f, 0.4f, 0.9f);
        private static readonly Color BlockBlue = new(0.3f, 0.6f, 0.9f, 1f);

        private const float HPBarWidth = 3.2f;
        private const float HPFillHeight = 0.3f;

        private Sprite _pixel;

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
#if UNITY_EDITOR
                DestroyImmediate(transform.GetChild(i).gameObject);
#else
                Destroy(transform.GetChild(i).gameObject);
#endif
            }
        }

        public void Build()
        {
            Clear();
            _pixel = CreatePixelSprite();

            BuildBackground();
            BuildHeroSprite();
            BuildVillainSprite();
            BuildTopBar();
            BuildPlayerHPBar();
            BuildEnemyArea();
            BuildEnergyOrb();
            BuildCardHand();
            BuildEndTurnButton();
            BuildDrawPile();
            BuildDiscardPile();
        }

        // ─── BACKGROUND ────────────────────────────────────────────

        private void BuildBackground()
        {
            var go = CreateChild("Background", new Vector3(0, 0, 5));
            BackgroundRenderer = go.AddComponent<SpriteRenderer>();
            BackgroundRenderer.sortingOrder = -100;

            if (_backgroundSprite != null)
            {
                BackgroundRenderer.sprite = _backgroundSprite;
                FitSpriteToCamera(BackgroundRenderer);
            }
            else
            {
                BackgroundRenderer.sprite = _pixel;
                BackgroundRenderer.color = new Color(0.08f, 0.06f, 0.1f, 1f);
                go.transform.localScale = new Vector3(20, 12, 1);
            }
        }

        // ─── HERO ──────────────────────────────────────────────────

        private void BuildHeroSprite()
        {
            var go = CreateChild("HeroSprite", new Vector3(-3.5f, -0.3f, 0));
            HeroRenderer = go.AddComponent<SpriteRenderer>();
            HeroRenderer.sortingOrder = 10;

            if (_heroSprite != null)
            {
                HeroRenderer.sprite = _heroSprite;
                FitSpriteToSize(HeroRenderer, 3f, 4f);
            }
            else
            {
                HeroRenderer.sprite = _pixel;
                HeroRenderer.color = new Color(0.3f, 0.25f, 0.2f, 0.6f);
                go.transform.localScale = new Vector3(3, 4, 1);
                CreateWorldText(go.transform, "Label", "HERO", 5f,
                    new Color(0.7f, 0.6f, 0.5f, 0.8f), Vector3.zero, 20);
            }
        }

        // ─── VILLAIN ───────────────────────────────────────────────

        private void BuildVillainSprite()
        {
            var go = CreateChild("VillainSprite", new Vector3(4f, -0.2f, 0));
            VillainRenderer = go.AddComponent<SpriteRenderer>();
            VillainRenderer.sortingOrder = 10;

            if (_villainSprite != null)
            {
                VillainRenderer.sprite = _villainSprite;
                FitSpriteToSize(VillainRenderer, 3f, 4f);
            }
            else
            {
                VillainRenderer.sprite = _pixel;
                VillainRenderer.color = new Color(0.35f, 0.15f, 0.2f, 0.6f);
                go.transform.localScale = new Vector3(3, 4, 1);
                CreateWorldText(go.transform, "Label", "VILLAIN", 5f,
                    new Color(0.8f, 0.4f, 0.4f, 0.8f), Vector3.zero, 20);
            }
        }

        // ─── TOP BAR ───────────────────────────────────────────────

        private void BuildTopBar()
        {
            var bar = CreateChild("TopBar", new Vector3(0, 4.5f, 0));
            var barSr = bar.AddComponent<SpriteRenderer>();
            barSr.sprite = _pixel;
            barSr.color = DarkBg;
            barSr.sortingOrder = 50;
            bar.transform.localScale = new Vector3(20, 0.7f, 1);

            float y = 4.5f;

            PlayerNameText = CreateWorldText(transform, "PlayerName",
                "Metha  the Ironclad", 3.5f, Color.white,
                new Vector3(-7.5f, y, 0), 60);

            CreateColoredQuad(transform, "HPIcon", HPRed,
                new Vector3(-3.8f, y, 0), new Vector3(0.25f, 0.25f, 1), 55);

            PlayerHPBarText = CreateWorldText(transform, "PlayerHPTopText",
                "13/72", 3f, HPRed,
                new Vector3(-3.0f, y, 0), 60);

            CreateColoredQuad(transform, "GoldIcon", GoldYellow,
                new Vector3(-1.8f, y, 0), new Vector3(0.25f, 0.25f, 1), 55);

            GoldText = CreateWorldText(transform, "GoldText",
                "324", 3f, GoldYellow,
                new Vector3(-1.1f, y, 0), 60);

            CreateColoredQuad(transform, "FloorIcon", Color.white,
                new Vector3(7.5f, y, 0), new Vector3(0.25f, 0.25f, 1), 55);

            FloorText = CreateWorldText(transform, "FloorText",
                "17", 3f, Color.white,
                new Vector3(8.2f, y, 0), 60);
        }

        // ─── PLAYER HP BAR ─────────────────────────────────────────

        private void BuildPlayerHPBar()
        {
            var container = CreateChild("PlayerHPBar", new Vector3(-3.5f, -2.7f, 0));

            CreateColoredQuad(container.transform, "HPBarBG",
                new Color(0.2f, 0.05f, 0.05f, 0.9f),
                Vector3.zero, new Vector3(3.2f, 0.35f, 1), 30);

            var fillGo = CreateColoredQuad(container.transform, "HPFill", HPRed,
                new Vector3(-1.31f, 0, 0), new Vector3(0.58f, 0.3f, 1), 31);
            PlayerHPFill = fillGo.GetComponent<SpriteRenderer>();

            PlayerHPBarText = CreateWorldText(container.transform, "HPText",
                "13/72", 2.5f, Color.white, new Vector3(0, 0, -0.01f), 35);
        }

        // ─── ENEMY AREA ────────────────────────────────────────────

        private void BuildEnemyArea()
        {
            // Intent above enemy
            var intentGo = CreateChild("EnemyIntent", new Vector3(4f, 2.8f, 0));
            var intentBgGo = CreateColoredQuad(intentGo.transform, "IntentBG",
                new Color(0.8f, 0.2f, 0.2f, 0.8f),
                Vector3.zero, new Vector3(0.8f, 0.55f, 1), 30);
            EnemyIntentBG = intentBgGo.GetComponent<SpriteRenderer>();

            EnemyIntentText = CreateWorldText(intentGo.transform, "IntentText",
                "14", 3.5f, Color.white, new Vector3(0, 0, -0.01f), 35);

            // HP bar below enemy
            var hpContainer = CreateChild("EnemyHPBar", new Vector3(4f, -2.7f, 0));

            CreateColoredQuad(hpContainer.transform, "HPBarBG",
                new Color(0.05f, 0.15f, 0.05f, 0.9f),
                Vector3.zero, new Vector3(3.2f, 0.35f, 1), 30);

            var fillGo = CreateColoredQuad(hpContainer.transform, "HPFill", HPGreen,
                Vector3.zero, new Vector3(3.1f, 0.3f, 1), 31);
            EnemyHPFill = fillGo.GetComponent<SpriteRenderer>();

            EnemyHPBarText = CreateWorldText(hpContainer.transform, "HPText",
                "49/49", 2.5f, Color.white, new Vector3(0, 0, -0.01f), 35);
        }

        // ─── ENERGY ORB ────────────────────────────────────────────

        private void BuildEnergyOrb()
        {
            var orb = CreateChild("EnergyOrb", new Vector3(-7.8f, -2.8f, 0));

            CreateColoredQuad(orb.transform, "OrbBG",
                new Color(0.15f, 0.08f, 0.02f, 1f),
                Vector3.zero, new Vector3(1.2f, 1.2f, 1), 40);

            CreateColoredQuad(orb.transform, "OrbRing", EnergyOrange,
                Vector3.zero, new Vector3(1.05f, 1.05f, 1), 41);

            CreateColoredQuad(orb.transform, "OrbInner",
                new Color(0.2f, 0.1f, 0.02f, 1f),
                Vector3.zero, new Vector3(0.85f, 0.85f, 1), 42);

            EnergyText = CreateWorldText(orb.transform, "EnergyText",
                $"{_currentEnergy}/{_maxEnergy}", 4f, EnergyOrange,
                new Vector3(0, 0, -0.01f), 45);
        }

        // ─── CARD HAND ─────────────────────────────────────────────

        private void BuildCardHand()
        {
            var hand = CreateChild("CardHand", new Vector3(0, -4.2f, 0));
            CardHandParent = hand.transform;

            CardSlots = new Transform[_cardCount];
            CardNameTexts = new TextMeshPro[_cardCount];
            CardCostTexts = new TextMeshPro[_cardCount];
            CardArtRenderers = new SpriteRenderer[_cardCount];

            string[] names = { "Bash", "Spot Weakness", "Strike", "Strike", "Strike" };
            int[] costs = { 2, 1, 1, 1, 1 };

            float cardW = 1.6f;
            float spacing = 0.15f;
            float totalW = _cardCount * cardW + (_cardCount - 1) * spacing;
            float startX = -totalW / 2f + cardW / 2f;

            for (int i = 0; i < _cardCount; i++)
            {
                float x = startX + i * (cardW + spacing);
                string cName = i < names.Length ? names[i] : $"Card {i + 1}";
                int cCost = i < costs.Length ? costs[i] : 1;
                CardSlots[i] = BuildCard(hand.transform, cName, cCost, i + 1, x);
            }
        }

        private Transform BuildCard(Transform parent, string cardName, int cost, int slotNum, float x)
        {
            float cardW = 1.6f;
            float cardH = 2.3f;
            var card = new GameObject($"Card_{slotNum}");
            card.transform.SetParent(parent, false);
            card.transform.localPosition = new Vector3(x, 0, 0);

            // Card background
            CreateColoredQuad(card.transform, "CardBG", CardBg,
                Vector3.zero, new Vector3(cardW, cardH, 1), 50);

            // Border
            CreateColoredQuad(card.transform, "CardBorder", CardBorder,
                Vector3.zero, new Vector3(cardW + 0.06f, cardH + 0.06f, 1), 49);

            // Slot number above card
            CreateWorldText(parent, $"SlotLabel_{slotNum}",
                slotNum.ToString(), 2.5f, new Color(0.7f, 0.7f, 0.7f),
                new Vector3(x, cardH / 2f + 0.25f, 0), 55);

            // Card art area
            var artGo = CreateColoredQuad(card.transform, "CardArt",
                new Color(0.6f, 0.3f, 0.15f, 1f),
                new Vector3(0, 0.05f, -0.01f), new Vector3(cardW * 0.8f, cardH * 0.42f, 1), 52);
            CardArtRenderers[slotNum - 1] = artGo.GetComponent<SpriteRenderer>();

            // Card name
            CardNameTexts[slotNum - 1] = CreateWorldText(card.transform, "CardName",
                cardName, 2f, Color.white,
                new Vector3(0, cardH * 0.37f, -0.01f), 55);

            // Card type
            CreateWorldText(card.transform, "CardType", "Attack", 1.5f,
                new Color(0.7f, 0.6f, 0.5f),
                new Vector3(0, -cardH * 0.4f, -0.01f), 55);

            // Cost orb
            CreateColoredQuad(card.transform, "CostOrbBG", EnergyOrange,
                new Vector3(-cardW * 0.38f, cardH * 0.38f, -0.02f),
                new Vector3(0.42f, 0.42f, 1), 53);

            CardCostTexts[slotNum - 1] = CreateWorldText(card.transform, "CostText",
                cost.ToString(), 2.8f, Color.white,
                new Vector3(-cardW * 0.38f, cardH * 0.38f, -0.03f), 54);

            return card.transform;
        }

        // ─── END TURN ──────────────────────────────────────────────

        private void BuildEndTurnButton()
        {
            var btn = CreateChild("EndTurnButton", new Vector3(7f, -3f, 0));

            CreateColoredQuad(btn.transform, "BtnBG", SlotGray,
                Vector3.zero, new Vector3(2f, 0.7f, 1), 50);

            CreateColoredQuad(btn.transform, "BtnBorder",
                new Color(0.6f, 0.55f, 0.45f),
                Vector3.zero, new Vector3(2.08f, 0.78f, 1), 49);

            EndTurnText = CreateWorldText(btn.transform, "BtnText",
                "End Turn", 3f, Color.white,
                new Vector3(0, 0, -0.01f), 55);

            btn.AddComponent<BoxCollider2D>().size = new Vector2(2f, 0.7f);
        }

        // ─── DRAW / DISCARD ────────────────────────────────────────

        private void BuildDrawPile()
        {
            var pile = CreateChild("DrawPile", new Vector3(-8.2f, -4.3f, 0));

            CreateColoredQuad(pile.transform, "PileBG",
                new Color(0.5f, 0.15f, 0.1f, 0.9f),
                Vector3.zero, new Vector3(0.8f, 0.8f, 1), 50);

            DrawPileText = CreateWorldText(pile.transform, "Count",
                "12", 3f, Color.white,
                new Vector3(0, 0, -0.01f), 55);
        }

        private void BuildDiscardPile()
        {
            var pile = CreateChild("DiscardPile", new Vector3(8.2f, -4.3f, 0));

            CreateColoredQuad(pile.transform, "PileBG",
                new Color(0.15f, 0.35f, 0.15f, 0.9f),
                Vector3.zero, new Vector3(0.8f, 0.8f, 1), 50);

            DiscardPileText = CreateWorldText(pile.transform, "Count",
                "0", 3f, Color.white,
                new Vector3(0, 0, -0.01f), 55);
        }

        // ─── DYNAMIC UPDATES (called by CombatPresenter) ────────────

        public void UpdatePlayerHP(int current, int max, int block)
        {
            string text = block > 0 ? $"{current}/{max} ({block})" : $"{current}/{max}";
            if (PlayerHPBarText != null) PlayerHPBarText.text = text;
            if (PlayerHPFill != null)
            {
                float ratio = max > 0 ? (float)current / max : 0;
                float fillW = HPBarWidth * ratio;
                PlayerHPFill.transform.localScale = new Vector3(fillW, HPFillHeight, 1);
                PlayerHPFill.transform.localPosition = new Vector3(-(HPBarWidth - fillW) / 2f, 0, 0);
                PlayerHPFill.color = block > 0 ? BlockBlue : HPRed;
            }
        }

        public void UpdateEnemyHP(int current, int max, int block)
        {
            string text = block > 0 ? $"{current}/{max} ({block})" : $"{current}/{max}";
            if (EnemyHPBarText != null) EnemyHPBarText.text = text;
            if (EnemyHPFill != null)
            {
                float ratio = max > 0 ? (float)current / max : 0;
                float fillW = HPBarWidth * ratio;
                EnemyHPFill.transform.localScale = new Vector3(fillW, HPFillHeight, 1);
                EnemyHPFill.transform.localPosition = new Vector3(-(HPBarWidth - fillW) / 2f, 0, 0);
            }
        }

        public void UpdateEnergy(int current, int max)
        {
            if (EnergyText != null) EnergyText.text = $"{current}/{max}";
        }

        public void UpdateIntent(IntentType type, int value)
        {
            if (EnemyIntentText != null)
                EnemyIntentText.text = value.ToString();
            if (EnemyIntentBG != null)
                EnemyIntentBG.color = type == IntentType.Attack
                    ? new Color(0.8f, 0.2f, 0.2f, 0.8f)
                    : new Color(0.2f, 0.5f, 0.8f, 0.8f);
        }

        public void UpdateDrawPile(int count)
        {
            if (DrawPileText != null) DrawPileText.text = count.ToString();
        }

        public void UpdateDiscardPile(int count)
        {
            if (DiscardPileText != null) DiscardPileText.text = count.ToString();
        }

        public void UpdatePlayerName(string heroName)
        {
            if (PlayerNameText != null) PlayerNameText.text = heroName;
        }

        public void UpdateGold(int amount)
        {
            if (GoldText != null) GoldText.text = amount.ToString();
        }

        public void SetHeroSprite(Sprite s)
        {
            if (HeroRenderer == null) return;
            if (s != null)
            {
                HeroRenderer.sprite = s;
                HeroRenderer.color = Color.white;
                FitSpriteToSize(HeroRenderer, 3f, 4f);
            }
        }

        public void SetVillainSprite(Sprite s)
        {
            if (VillainRenderer == null) return;
            if (s != null)
            {
                VillainRenderer.sprite = s;
                VillainRenderer.color = Color.white;
                FitSpriteToSize(VillainRenderer, 3f, 4f);
            }
        }

        public void UpdateHand(List<CardDisplayData> cards)
        {
            if (CardHandParent == null) return;

            for (int i = CardHandParent.childCount - 1; i >= 0; i--)
                Destroy(CardHandParent.GetChild(i).gameObject);

            if (_pixel == null) _pixel = CreatePixelSprite();

            int count = cards.Count;
            CardSlots = new Transform[count];
            CardNameTexts = new TextMeshPro[count];
            CardCostTexts = new TextMeshPro[count];
            CardArtRenderers = new SpriteRenderer[count];

            float cardW = 1.6f;
            float spacing = 0.15f;
            float totalW = count * cardW + Mathf.Max(0, count - 1) * spacing;
            float startX = -totalW / 2f + cardW / 2f;

            for (int i = 0; i < count; i++)
            {
                float x = startX + i * (cardW + spacing);
                var data = cards[i];
                CardSlots[i] = BuildCard(CardHandParent, data.Name, data.Cost, i + 1, x);

                if (data.Art != null && CardArtRenderers[i] != null)
                {
                    CardArtRenderers[i].sprite = data.Art;
                    CardArtRenderers[i].color = Color.white;
                }

                var collider = CardSlots[i].gameObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(cardW, 2.3f);
            }
        }

        public void SetInteractable(bool interactable)
        {
            float alpha = interactable ? 1f : 0.5f;
            if (EndTurnText != null)
                EndTurnText.color = new Color(1, 1, 1, alpha);
        }

        // ─── HELPERS ───────────────────────────────────────────────

        private GameObject CreateChild(string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            return go;
        }

        private GameObject CreateColoredQuad(Transform parent, string name, Color color,
            Vector3 localPos, Vector3 localScale, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _pixel;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        private static TextMeshPro CreateWorldText(Transform parent, string name,
            string text, float fontSize, Color color, Vector3 localPos, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.sortingOrder = sortingOrder;

            var rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(4f, 1.5f);

            return tmp;
        }

        private static Sprite CreatePixelSprite()
        {
            var tex = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }

        private static void FitSpriteToCamera(SpriteRenderer sr)
        {
            var cam = Camera.main;
            if (cam == null || sr.sprite == null) return;
            float worldH = cam.orthographicSize * 2f;
            float worldW = worldH * cam.aspect;
            var bounds = sr.sprite.bounds.size;
            float scaleX = worldW / bounds.x;
            float scaleY = worldH / bounds.y;
            float scale = Mathf.Max(scaleX, scaleY);
            sr.transform.localScale = new Vector3(scale, scale, 1);
        }

        private static void FitSpriteToSize(SpriteRenderer sr, float maxW, float maxH)
        {
            if (sr.sprite == null) return;
            var bounds = sr.sprite.bounds.size;
            float scale = Mathf.Min(maxW / bounds.x, maxH / bounds.y);
            sr.transform.localScale = new Vector3(scale, scale, 1);
        }
    }
}
