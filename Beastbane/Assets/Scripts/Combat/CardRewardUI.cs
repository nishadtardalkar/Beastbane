using Beastbane.Data;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beastbane.Combat
{
    /// <summary>
    /// Displays 3 reward cards after winning combat.
    /// Only the fighter can pick; spectators just watch.
    /// Attach to the same scene child as CombatUIBuilder (CombatScene).
    /// </summary>
    public class CardRewardUI : MonoBehaviour
    {
        [SerializeField] private GameDatabase _db;

        private CombatManager _combat;
        private Transform _rewardRoot;
        private Transform[] _rewardCards;
        private bool _showing;

        private void LateUpdate()
        {
            if (_combat == null)
            {
                _combat = FindAnyObjectByType<CombatManager>();
                if (_combat == null) return;
            }

            if (_combat.Phase == CombatPhase.Reward && !_showing)
            {
                ShowRewards();
                _showing = true;
            }
            else if (_combat.Phase != CombatPhase.Reward && _showing)
            {
                HideRewards();
                _showing = false;
            }

            if (_showing)
                HandleInput();
        }

        private void ShowRewards()
        {
            if (_rewardRoot != null)
                Destroy(_rewardRoot.gameObject);

            var root = new GameObject("RewardPanel");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            _rewardRoot = root.transform;

            // Dim overlay
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(_rewardRoot, false);
            overlay.transform.localPosition = new Vector3(0, 0, -0.5f);
            overlay.transform.localScale = new Vector3(20, 12, 1);
            var overlSr = overlay.AddComponent<SpriteRenderer>();
            overlSr.sprite = GetPixel();
            overlSr.color = new Color(0, 0, 0, 0.7f);
            overlSr.sortingOrder = 90;

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_rewardRoot, false);
            titleGo.transform.localPosition = new Vector3(0, 3f, -1f);
            var titleTmp = titleGo.AddComponent<TextMeshPro>();
            titleTmp.text = "Choose a Card Reward";
            titleTmp.fontSize = 5f;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.sortingOrder = 100;
            titleTmp.rectTransform.sizeDelta = new Vector2(10, 2);

            // Skip button
            var skipGo = new GameObject("SkipButton");
            skipGo.transform.SetParent(_rewardRoot, false);
            skipGo.transform.localPosition = new Vector3(0, -3.5f, -1f);

            var skipBg = new GameObject("BG");
            skipBg.transform.SetParent(skipGo.transform, false);
            skipBg.transform.localScale = new Vector3(2.5f, 0.6f, 1);
            var skipBgSr = skipBg.AddComponent<SpriteRenderer>();
            skipBgSr.sprite = GetPixel();
            skipBgSr.color = new Color(0.4f, 0.3f, 0.3f, 0.9f);
            skipBgSr.sortingOrder = 95;

            var skipTextGo = new GameObject("Text");
            skipTextGo.transform.SetParent(skipGo.transform, false);
            skipTextGo.transform.localPosition = new Vector3(0, 0, -0.01f);
            var skipTmp = skipTextGo.AddComponent<TextMeshPro>();
            skipTmp.text = "Skip";
            skipTmp.fontSize = 3f;
            skipTmp.color = Color.white;
            skipTmp.alignment = TextAlignmentOptions.Center;
            skipTmp.sortingOrder = 100;
            skipTmp.rectTransform.sizeDelta = new Vector2(3, 1);

            skipGo.AddComponent<BoxCollider2D>().size = new Vector2(2.5f, 0.6f);

            // Reward cards
            int count = _combat.rewardCards.Count;
            _rewardCards = new Transform[count];
            float spacing = 3.5f;
            float totalW = (count - 1) * spacing;
            float startX = -totalW / 2f;

            for (int i = 0; i < count; i++)
            {
                int cardIdx = _combat.rewardCards[i];
                var card = _db != null ? _db.GetCard(cardIdx) : null;
                float x = startX + i * spacing;

                var cardGo = new GameObject($"RewardCard_{i}");
                cardGo.transform.SetParent(_rewardRoot, false);
                cardGo.transform.localPosition = new Vector3(x, 0, -1f);
                _rewardCards[i] = cardGo.transform;

                // Card BG
                var bg = new GameObject("CardBG");
                bg.transform.SetParent(cardGo.transform, false);
                bg.transform.localScale = new Vector3(2.5f, 3.5f, 1);
                var bgSr = bg.AddComponent<SpriteRenderer>();
                bgSr.sprite = GetPixel();
                bgSr.color = new Color(0.18f, 0.14f, 0.12f, 1f);
                bgSr.sortingOrder = 95;

                // Border
                var border = new GameObject("Border");
                border.transform.SetParent(cardGo.transform, false);
                border.transform.localScale = new Vector3(2.6f, 3.6f, 1);
                var borderSr = border.AddComponent<SpriteRenderer>();
                borderSr.sprite = GetPixel();
                borderSr.color = new Color(0.55f, 0.4f, 0.2f, 1f);
                borderSr.sortingOrder = 94;

                string cName = card != null ? card.cardName : $"Card {cardIdx}";
                string cDesc = card != null ? card.description : "";
                int cCost = card != null ? card.manaCost : 0;

                // Name
                var nameGo = new GameObject("Name");
                nameGo.transform.SetParent(cardGo.transform, false);
                nameGo.transform.localPosition = new Vector3(0, 1.2f, -0.01f);
                var nameTmp = nameGo.AddComponent<TextMeshPro>();
                nameTmp.text = cName;
                nameTmp.fontSize = 2.5f;
                nameTmp.color = Color.white;
                nameTmp.alignment = TextAlignmentOptions.Center;
                nameTmp.sortingOrder = 100;
                nameTmp.rectTransform.sizeDelta = new Vector2(2.2f, 1);

                // Description
                var descGo = new GameObject("Desc");
                descGo.transform.SetParent(cardGo.transform, false);
                descGo.transform.localPosition = new Vector3(0, -0.2f, -0.01f);
                var descTmp = descGo.AddComponent<TextMeshPro>();
                descTmp.text = cDesc;
                descTmp.fontSize = 1.8f;
                descTmp.color = new Color(0.8f, 0.75f, 0.65f);
                descTmp.alignment = TextAlignmentOptions.Center;
                descTmp.sortingOrder = 100;
                descTmp.rectTransform.sizeDelta = new Vector2(2.2f, 2);

                // Cost
                var costBg = new GameObject("CostOrb");
                costBg.transform.SetParent(cardGo.transform, false);
                costBg.transform.localPosition = new Vector3(-1f, 1.4f, -0.02f);
                costBg.transform.localScale = new Vector3(0.5f, 0.5f, 1);
                var costSr = costBg.AddComponent<SpriteRenderer>();
                costSr.sprite = GetPixel();
                costSr.color = new Color(0.9f, 0.55f, 0.1f, 1f);
                costSr.sortingOrder = 98;

                var costTextGo = new GameObject("CostText");
                costTextGo.transform.SetParent(cardGo.transform, false);
                costTextGo.transform.localPosition = new Vector3(-1f, 1.4f, -0.03f);
                var costTmp = costTextGo.AddComponent<TextMeshPro>();
                costTmp.text = cCost.ToString();
                costTmp.fontSize = 3f;
                costTmp.color = Color.white;
                costTmp.alignment = TextAlignmentOptions.Center;
                costTmp.sortingOrder = 100;
                costTmp.rectTransform.sizeDelta = new Vector2(1, 1);

                cardGo.AddComponent<BoxCollider2D>().size = new Vector2(2.5f, 3.5f);
            }
        }

        private void HideRewards()
        {
            if (_rewardRoot != null)
                Destroy(_rewardRoot.gameObject);
            _rewardRoot = null;
            _rewardCards = null;
        }

        private void HandleInput()
        {
            if (!IsFighter()) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector2 worldPos = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            var hit = Physics2D.OverlapPoint(worldPos);
            if (hit == null) return;

            // Skip button
            if (hit.transform.name == "SkipButton")
            {
                _combat.CmdSkipReward();
                return;
            }

            // Reward card selection
            if (_rewardCards == null) return;
            for (int i = 0; i < _rewardCards.Length; i++)
            {
                if (_rewardCards[i] != null && hit.transform == _rewardCards[i])
                {
                    _combat.CmdPickReward(i);
                    return;
                }
            }
        }

        private bool IsFighter()
        {
            return _combat != null && _combat.IsMyFight;
        }

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
    }
}
