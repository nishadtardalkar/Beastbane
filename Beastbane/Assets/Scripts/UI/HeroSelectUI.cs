using Beastbane.Data;
using Beastbane.Netcode;
using Mirror;
using TMPro;
using UnityEngine;

namespace Beastbane.UI
{
    /// <summary>
    /// Displays hero selection screen using world-space sprites.
    /// Attach to a GameObject under the SceneSwitcher "HeroSelect" scene child.
    /// Assign the GameDatabase in the Inspector.
    /// </summary>
    public class HeroSelectUI : MonoBehaviour
    {
        [SerializeField] private GameDatabase _db;
        [SerializeField] private float _heroSpacing = 4f;

        private bool _selected;
        private Transform[] _heroCards;

        private void Start()
        {
            if (_db == null || _db.heroes.Length == 0)
            {
                Debug.LogWarning("HeroSelectUI: No heroes in database.");
                return;
            }

            BuildHeroCards();
        }

        private void BuildHeroCards()
        {
            int count = _db.heroes.Length;
            float totalW = (count - 1) * _heroSpacing;
            float startX = -totalW / 2f;

            _heroCards = new Transform[count];

            for (int i = 0; i < count; i++)
            {
                var hero = _db.heroes[i];
                float x = startX + i * _heroSpacing;

                var card = new GameObject($"HeroCard_{i}");
                card.transform.SetParent(transform, false);
                card.transform.localPosition = new Vector3(x, 0, 0);
                _heroCards[i] = card.transform;

                // Background panel
                var bgGo = CreateQuad(card.transform, "BG",
                    new Color(0.15f, 0.12f, 0.18f, 0.9f),
                    Vector3.zero, new Vector3(3f, 5f, 1), 10);
                var borderGo = CreateQuad(card.transform, "Border",
                    new Color(0.5f, 0.4f, 0.2f, 1f),
                    Vector3.zero, new Vector3(3.1f, 5.1f, 1), 9);

                // Hero sprite
                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(card.transform, false);
                spriteGo.transform.localPosition = new Vector3(0, 0.5f, -0.01f);
                var sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 15;
                if (hero.sprite != null)
                {
                    sr.sprite = hero.sprite;
                    FitSprite(sr, 2.2f, 2.5f);
                }
                else
                {
                    sr.sprite = GetPixel();
                    sr.color = new Color(0.3f, 0.25f, 0.2f, 0.6f);
                    spriteGo.transform.localScale = new Vector3(2, 2.5f, 1);
                }

                // Hero name
                CreateText(card.transform, "Name", hero.heroName, 3f, Color.white,
                    new Vector3(0, -1.5f, -0.01f), 20);

                // Stats
                CreateText(card.transform, "Stats",
                    $"HP: {hero.maxHP}  Energy: {hero.startEnergy}\nCards: {hero.startingDeck.Count}",
                    2f, new Color(0.8f, 0.75f, 0.65f),
                    new Vector3(0, -2.2f, -0.01f), 20);

                // Collider for click
                var col = card.AddComponent<BoxCollider2D>();
                col.size = new Vector2(3f, 5f);
            }

            // Title
            CreateText(transform, "Title", "Choose Your Hero", 5f, Color.white,
                new Vector3(0, 3.8f, 0), 20);
        }

        private void Update()
        {
            if (_selected) return;
            if (_heroCards == null) return;
            if (!Input.GetMouseButtonDown(0)) return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector2 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
            var hit = Physics2D.OverlapPoint(worldPos);
            if (hit == null) return;

            for (int i = 0; i < _heroCards.Length; i++)
            {
                if (hit.transform != _heroCards[i]) continue;

                SelectHero(i);
                return;
            }
        }

        private void SelectHero(int index)
        {
            var runData = FindLocalPlayerRunData();
            if (runData == null)
            {
                Debug.LogWarning("HeroSelectUI: PlayerRunData not found for local player.");
                return;
            }

            runData.CmdSelectHero(index);
            _selected = true;
            Debug.Log($"HeroSelectUI: Selected hero {index} ({_db.heroes[index].heroName})");
        }

        private static PlayerRunData FindLocalPlayerRunData()
        {
            foreach (var prd in FindObjectsByType<PlayerRunData>(FindObjectsSortMode.None))
            {
                if (prd.isOwned) return prd;
            }
            return null;
        }

        // ── Helpers ─────────────────────────────────────────────────

        private static Sprite _pixel;

        private static Sprite GetPixel()
        {
            if (_pixel != null) return _pixel;
            var tex = new Texture2D(4, 4);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            _pixel = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            return _pixel;
        }

        private static GameObject CreateQuad(Transform parent, string objName, Color color,
            Vector3 localPos, Vector3 localScale, int sortingOrder)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetPixel();
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        private static void CreateText(Transform parent, string objName, string text,
            float fontSize, Color color, Vector3 localPos, int sortingOrder)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.sortingOrder = sortingOrder;
            tmp.rectTransform.sizeDelta = new Vector2(3f, 2f);
        }

        private static void FitSprite(SpriteRenderer sr, float maxW, float maxH)
        {
            if (sr.sprite == null) return;
            var b = sr.sprite.bounds.size;
            float s = Mathf.Min(maxW / b.x, maxH / b.y);
            sr.transform.localScale = new Vector3(s, s, 1);
        }
    }
}
