using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
#endif

#if STEAMWORKS_NET
using Steamworks;
#endif

namespace Beastbane.Steam
{
    /// <summary>
    /// Runtime UI that shows current Steam lobby members.
    /// Attach to any GameObject (prefab-friendly).
    /// </summary>
    public sealed class SteamLobbyMembersUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private int canvasSortingOrder = 500;

        [Header("Refresh")]
        [SerializeField, Min(0.1f)] private float refreshIntervalSeconds = 0.5f;

        private GameObject _root;
        private Text _headerText;
        private RectTransform _contentRoot;
        private readonly List<Text> _memberLines = new List<Text>(16);

        private float _nextRefreshAt;

        private void Awake()
        {
            if (!buildOnAwake) return;
            EnsureEventSystem();
            BuildUI();
        }

        private void OnEnable()
        {
            var mgr = SteamLobbyManager.Instance;
            if (mgr == null) return;
            mgr.LobbyEntered += OnLobbyChanged;
            mgr.LobbyLeft += OnLobbyLeft;
        }

        private void OnDisable()
        {
            var mgr = SteamLobbyManager.Instance;
            if (mgr == null) return;
            mgr.LobbyEntered -= OnLobbyChanged;
            mgr.LobbyLeft -= OnLobbyLeft;
        }

        private void Update()
        {
            if (_root == null) return;
            if (Time.unscaledTime < _nextRefreshAt) return;
            _nextRefreshAt = Time.unscaledTime + refreshIntervalSeconds;
            Refresh();
        }

        private void OnLobbyChanged(
#if STEAMWORKS_NET
            CSteamID _
#else
            object _
#endif
        )
        {
            Refresh(true);
        }

        private void OnLobbyLeft()
        {
            Refresh(true);
        }

        public void Refresh(bool force = false)
        {
            if (_root == null) return;
            if (force) _nextRefreshAt = Time.unscaledTime;

            var mgr = SteamLobbyManager.Instance;
            if (mgr == null || !mgr.InLobby)
            {
                if (_headerText != null) _headerText.text = "Lobby: (not in lobby)";
                SetMemberLineCount(0);
                return;
            }

            var lobbyId = mgr.CurrentLobbyId;
            var ownerId = mgr.LobbyOwnerId;
            var count = mgr.GetLobbyMemberCount();

            if (_headerText != null)
            {
                _headerText.text = $"Lobby: {lobbyId}\nOwner: {ownerId}\nMembers: {count}";
            }

            SetMemberLineCount(count);

            for (var i = 0; i < count; i++)
            {
                var memberId = mgr.GetLobbyMemberByIndex(i);
                var display = memberId.ToString();

#if STEAMWORKS_NET
                // SteamFriends.GetFriendPersonaName works for self/friends; for non-friends it can be limited.
                var name = SteamFriends.GetFriendPersonaName(memberId);
                if (!string.IsNullOrWhiteSpace(name)) display = $"{name} ({memberId})";
                if (memberId == ownerId) display = $"[HOST] {display}";
#endif

                _memberLines[i].text = display;
            }
        }

        private void SetMemberLineCount(int count)
        {
            while (_memberLines.Count < count)
            {
                var t = CreateText(_contentRoot, $"MemberLine_{_memberLines.Count}", 14, FontStyle.Normal);
                t.alignment = TextAnchor.MiddleLeft;
                t.color = new Color(0.90f, 0.92f, 0.96f, 1f);
                var rt = t.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(0f, 22f);
                _memberLines.Add(t);
            }

            for (var i = 0; i < _memberLines.Count; i++)
            {
                _memberLines[i].gameObject.SetActive(i < count);
            }
        }

        private void EnsureEventSystem()
        {
            var es = FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<EventSystem>();
                DontDestroyOnLoad(esGo);
            }

#if ENABLE_INPUT_SYSTEM
            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
#else
            if (es.GetComponent<StandaloneInputModule>() == null)
                es.gameObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private void BuildUI()
        {
            if (_root != null) return;

            _root = new GameObject("SteamLobbyMembersUI_Root");
            _root.transform.SetParent(transform, false);

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(_root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = CreateUIObject("Panel", canvasGo.transform);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.90f);

            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 1f);
            panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.pivot = new Vector2(1f, 1f);
            panelRt.anchoredPosition = new Vector2(-16f, -16f);
            panelRt.sizeDelta = new Vector2(420f, 320f);

            var vLayout = panel.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(14, 14, 14, 14);
            vLayout.spacing = 10;
            vLayout.childControlHeight = true;
            vLayout.childControlWidth = true;
            vLayout.childForceExpandHeight = false;
            vLayout.childForceExpandWidth = true;

            _headerText = CreateText(panel.transform, "Header", 13, FontStyle.Bold);
            _headerText.alignment = TextAnchor.UpperLeft;
            _headerText.color = Color.white;
            var headerRt = _headerText.GetComponent<RectTransform>();
            headerRt.sizeDelta = new Vector2(0f, 56f);

            var scrollGo = CreateUIObject("ScrollView", panel.transform);
            var scrollImage = scrollGo.AddComponent<Image>();
            scrollImage.color = new Color(0.12f, 0.13f, 0.16f, 1f);
            scrollImage.raycastTarget = true;

            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.sizeDelta = new Vector2(0f, 220f);

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            var viewport = CreateUIObject("Viewport", scrollGo.transform);
            var viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;

            var viewportRt = viewport.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(8f, 8f);
            viewportRt.offsetMax = new Vector2(-8f, -8f);

            var content = CreateUIObject("Content", viewport.transform);
            _contentRoot = content.GetComponent<RectTransform>();
            _contentRoot.anchorMin = new Vector2(0f, 1f);
            _contentRoot.anchorMax = new Vector2(1f, 1f);
            _contentRoot.pivot = new Vector2(0.5f, 1f);
            _contentRoot.anchoredPosition = Vector2.zero;
            _contentRoot.sizeDelta = new Vector2(0f, 0f);

            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 6;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRt;
            scrollRect.content = _contentRoot;

            Refresh(true);
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static Text CreateText(Transform parent, string name, int fontSize, FontStyle style)
        {
            var go = CreateUIObject(name, parent);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var rt = text.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 24f);
            return text;
        }
    }
}

