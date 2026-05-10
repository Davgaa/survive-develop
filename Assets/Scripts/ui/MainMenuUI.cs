using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet;

public class MainMenuUI : MonoBehaviour
{
    NetworkLauncher _launcher;

    TMP_Text _statusDot, _rttVal, _fpsVal, _playersVal, _serverVal;
    GameObject _serverPanel;
    bool _panelVisible = true;

    static readonly Color BG    = new Color(0.04f, 0.07f, 0.12f, 0.90f);
    static readonly Color BTN   = new Color(0.10f, 0.14f, 0.20f, 0.92f);
    static readonly Color GOLD  = new Color(0.95f, 0.65f, 0.13f, 1.00f);
    static readonly Color GREEN = new Color(0.18f, 0.85f, 0.40f, 1.00f);
    static readonly Color RED   = new Color(0.90f, 0.22f, 0.22f, 1.00f);
    static readonly Color DIM   = new Color(0.65f, 0.65f, 0.65f, 1.00f);

    void Awake()
    {
        _launcher = FindObjectOfType<NetworkLauncher>();
        BuildUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
        {
            _panelVisible = !_panelVisible;
            if (_serverPanel != null) _serverPanel.SetActive(_panelVisible);
        }
        RefreshMetrics();
    }

    void RefreshMetrics()
    {
        if (_fpsVal != null)
            _fpsVal.text = Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f)).ToString();

        bool online = InstanceFinder.NetworkManager != null &&
                      (InstanceFinder.ClientManager.Started || InstanceFinder.ServerManager.Started);

        if (_statusDot != null)
        {
            _statusDot.text = online ? "● ONLINE" : "● OFFLINE";
            _statusDot.color = online ? GREEN : DIM;
        }

        if (!online) return;

        if (_rttVal != null)
            _rttVal.text = $"{InstanceFinder.TimeManager.RoundTripTime} ms";

        if (_playersVal != null && InstanceFinder.ServerManager.Started)
            _playersVal.text = InstanceFinder.ServerManager.Clients.Count.ToString();

        if (_serverVal != null)
            _serverVal.text = InstanceFinder.ServerManager.Started ? "Running" : "Stopped";
    }

    void BuildUI()
    {
        var root = MakeCanvas("MainMenuCanvas", 10);

        // Server status panel — top left
        _serverPanel = MakeImage(root, "ServerPanel",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(226, 190), new Vector2(10, -10), BG);

        var sp = _serverPanel.transform;
        var hdr = Txt(sp, "SERVER STATUS", 10, new Vector2(10, -10), new Vector2(110, 18));
        hdr.fontStyle = FontStyles.Bold;

        _statusDot = Txt(sp, "● ONLINE", 10, new Vector2(120, -10), new Vector2(100, 18));
        _statusDot.color = GREEN;

        float ry = -32f;
        Row(sp, "Target",    "127.0.0.1:10000", ref ry, out _);
        Row(sp, "RTT",       "0 ms",            ref ry, out _rttVal);
        Row(sp, "Tick Rate", "30 Ticks/s",      ref ry, out _);
        Row(sp, "FPS",       "60",              ref ry, out _fpsVal);
        Row(sp, "Players",   "0",               ref ry, out _playersVal);
        Row(sp, "Server",    "Running",         ref ry, out _serverVal);

        // Title — center
        var title = TxtCenter(root, "Survive", 72, new Vector2(0, 130), new Vector2(700, 100));
        title.fontStyle = FontStyles.Bold;

        var sub = TxtCenter(root, "LOW-SPEC ONLINE SURVIVAL", 15, new Vector2(0, 72), new Vector2(600, 30));
        sub.color = DIM;
        sub.characterSpacing = 4f;

        // Buttons
        const float bh = 54f, gap = 8f;
        float by = 10f;

        var btnPlay = MakeBtn(root, "PLAY",        Color.white, BTN,  new Vector2(0, by));
        btnPlay.onClick.AddListener(() => _launcher?.OnPlayLocalClick());

        var btnMulti = MakeBtn(root, "MULTIPLAYER", Color.black, GOLD, new Vector2(0, by - (bh + gap)));
        btnMulti.onClick.AddListener(() => _launcher?.OnMultiplayerRenderClick());

        MakeBtn(root, "SETTINGS", Color.white, BTN, new Vector2(0, by - 2 * (bh + gap)));
        MakeBtn(root, "SCORE",    Color.white, BTN, new Vector2(0, by - 3 * (bh + gap)));

        var btnExit = MakeBtn(root, "EXIT", RED, BTN, new Vector2(0, by - 4 * (bh + gap)));
        btnExit.onClick.AddListener(Application.Quit);

        // Bottom labels
        var ver = Txt(sp: root, text: "VERSION 1.0.0", size: 12,
            pos: new Vector2(20, 20), size2: new Vector2(200, 24),
            anchorMin: new Vector2(0, 0), anchorMax: new Vector2(0, 0), pivot: new Vector2(0, 0));
        ver.color = DIM;

        var f3 = Txt(sp: root, text: "PRESS F3 TO TOGGLE METRICS", size: 11,
            pos: new Vector2(-20, 20), size2: new Vector2(340, 24),
            anchorMin: new Vector2(1, 0), anchorMax: new Vector2(1, 0), pivot: new Vector2(1, 0));
        f3.color = DIM;
        f3.alignment = TextAlignmentOptions.Right;
    }

    void Row(Transform parent, string label, string val, ref float y, out TMP_Text valText)
    {
        var lbl = Txt(parent, label, 10, new Vector2(10, y), new Vector2(85, 20));
        lbl.color = DIM;
        valText = Txt(parent, val, 10, new Vector2(98, y), new Vector2(120, 20));
        y -= 22f;
    }

    Button MakeBtn(Transform parent, string label, Color textCol, Color bgCol, Vector2 pos)
    {
        var go = new GameObject(label + "Btn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(340, 54);
        rt.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = bgCol;

        var btn = go.AddComponent<Button>();
        var cols = btn.colors;
        cols.normalColor    = bgCol;
        cols.highlightedColor = Color.Lerp(bgCol, Color.white, 0.15f);
        cols.pressedColor   = Color.Lerp(bgCol, Color.black, 0.25f);
        btn.colors = cols;
        btn.targetGraphic = img;

        var lbl = TxtCenter(go.transform, label, 15, Vector2.zero, new Vector2(340, 54));
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = textCol;

        return btn;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    Transform MakeCanvas(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var cv = go.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = order;
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go.transform;
    }

    GameObject MakeImage(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        go.AddComponent<Image>().color = color;
        return go;
    }

    // Top-left anchored text inside a panel (pos = offset from panel's top-left)
    TMP_Text Txt(Transform sp, string text, int size, Vector2 pos, Vector2 size2)
    {
        var go = new GameObject(text.Length > 20 ? text[..20] : text);
        go.transform.SetParent(sp, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = size2;
        rt.anchoredPosition = new Vector2(pos.x, pos.y);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = Color.white;
        t.overflowMode = TextOverflowModes.Overflow;
        t.alignment = TextAlignmentOptions.Left;
        return t;
    }

    // Fully custom anchor text
    TMP_Text Txt(Transform sp, string text, int size, Vector2 pos, Vector2 size2,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(text.Length > 20 ? text[..20] : text);
        go.transform.SetParent(sp, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = size2;
        rt.anchoredPosition = pos;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = Color.white;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    // Center-anchored text
    TMP_Text TxtCenter(Transform parent, string text, int size, Vector2 pos, Vector2 size2)
    {
        var go = new GameObject(text.Length > 20 ? text[..20] : text);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size2;
        rt.anchoredPosition = pos;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = Color.white;
        t.overflowMode = TextOverflowModes.Overflow;
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }
}
