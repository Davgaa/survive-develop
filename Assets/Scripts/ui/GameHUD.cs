using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet;

public class GameHUD : MonoBehaviour
{
    // Wave header
    TMP_Text _waveText;

    // Player stats
    Image _hpBar, _staminaBar, _armorBar;
    TMP_Text _hpVal, _staminaVal, _armorVal;

    // Network metrics (F3)
    TMP_Text _metricsText;
    GameObject _metricsPanel;
    bool _metricsVisible;

    // Top-right
    TMP_Text _pingText, _playersText;

    // Minimap
    RawImage _minimapImage;
    Texture2D _minimapTex;
    const int MAP_SIZE = 128;
    const float MAP_WORLD_RANGE = 150f;

    // Refs
    WaveManager _wave;
    PlayerHealth _localHealth;

    static readonly Color BG     = new Color(0.04f, 0.07f, 0.12f, 0.82f);
    static readonly Color HP_COL = new Color(0.88f, 0.20f, 0.20f, 1f);
    static readonly Color ST_COL = new Color(0.95f, 0.65f, 0.13f, 1f);
    static readonly Color AR_COL = new Color(0.20f, 0.55f, 0.90f, 1f);
    static readonly Color DIM    = new Color(0.65f, 0.65f, 0.65f, 1f);
    static readonly Color PANEL  = new Color(0.04f, 0.07f, 0.12f, 0.72f);

    void Awake()
    {
        _minimapTex = new Texture2D(MAP_SIZE, MAP_SIZE, TextureFormat.RGBA32, false);
        _minimapTex.filterMode = FilterMode.Point;
        BuildUI();
    }

    void Start()
    {
        _wave = FindObjectOfType<WaveManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
        {
            _metricsVisible = !_metricsVisible;
            if (_metricsPanel != null) _metricsPanel.SetActive(_metricsVisible);
        }

        // Find local player health once it spawns
        if (_localHealth == null)
        {
            foreach (var h in FindObjectsOfType<PlayerHealth>())
            {
                if (h.IsOwner) { _localHealth = h; break; }
            }
        }

        RefreshWave();
        RefreshStats();
        RefreshNetwork();
        RefreshMinimap();
    }

    void RefreshWave()
    {
        if (_wave == null || _waveText == null) return;
        _waveText.text = $"WAVE {_wave.CurrentWave}  |  ALIVE: {_wave.EnemiesAlive}";
    }

    void RefreshStats()
    {
        if (_localHealth == null) return;

        float hp = _localHealth.CurrentHp;
        float maxHp = _localHealth.MaxHp;

        SetBar(_hpBar, _hpVal, hp, maxHp);
        // Stamina/Armor: placeholder until those systems are added
    }

    void SetBar(Image bar, TMP_Text label, float cur, float max)
    {
        if (bar != null) bar.fillAmount = max > 0 ? cur / max : 0;
        if (label != null) label.text = $"{Mathf.RoundToInt(cur)}/{Mathf.RoundToInt(max)}";
    }

    void RefreshNetwork()
    {
        bool online = InstanceFinder.NetworkManager != null &&
                      (InstanceFinder.ClientManager.Started || InstanceFinder.ServerManager.Started);

        if (_pingText != null)
            _pingText.text = online ? $"Ping: {InstanceFinder.TimeManager.RoundTripTime} ms" : "Ping: --";

        if (_playersText != null && online && InstanceFinder.ServerManager.Started)
            _playersText.text = $"Players: {InstanceFinder.ServerManager.Clients.Count}/4";

        if (_metricsText != null && online)
        {
            _metricsText.text =
                $"RTT: {InstanceFinder.TimeManager.RoundTripTime} ms\n" +
                $"Tick: {InstanceFinder.TimeManager.TickRate} Hz\n" +
                $"FPS: {Mathf.RoundToInt(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f))}\n" +
                $"Server: {(InstanceFinder.ServerManager.Started ? "Local" : "Remote")}";
        }
    }

    void RefreshMinimap()
    {
        if (_minimapTex == null || _minimapImage == null) return;

        // Clear to dark
        var pixels = new Color32[MAP_SIZE * MAP_SIZE];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(10, 18, 28, 200);

        // Draw enemies (red dots)
        foreach (var e in FindObjectsOfType<EnemyController>())
            DrawDot(pixels, e.transform.position, new Color32(220, 50, 50, 255), 1);

        // Draw players (white triangles → just white dots)
        foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
            DrawDot(pixels, p.transform.position, new Color32(255, 255, 255, 255), 2);

        _minimapTex.SetPixels32(pixels);
        _minimapTex.Apply();
    }

    void DrawDot(Color32[] pixels, Vector3 worldPos, Color32 color, int radius)
    {
        int px = Mathf.RoundToInt((worldPos.x / MAP_WORLD_RANGE + 0.5f) * MAP_SIZE);
        int py = Mathf.RoundToInt((worldPos.z / MAP_WORLD_RANGE + 0.5f) * MAP_SIZE);

        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            int x = px + dx, y = py + dy;
            if (x >= 0 && x < MAP_SIZE && y >= 0 && y < MAP_SIZE)
                pixels[y * MAP_SIZE + x] = color;
        }
    }

    void BuildUI()
    {
        var root = MakeCanvas("GameHUDCanvas", 10);

        // ── Wave header (top center) ────────────────────────────────────
        var waveBox = MakeBox(root, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(420, 44), new Vector2(0, -10), BG);
        _waveText = TxtCenter(waveBox.transform, "WAVE 1  |  ALIVE: 0", 18,
            Vector2.zero, new Vector2(420, 44));
        _waveText.fontStyle = FontStyles.Bold;

        var objBox = MakeBox(root, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(260, 30), new Vector2(0, -62), new Color(0, 0, 0, 0.5f));
        TxtCenter(objBox.transform, "Survive the wave", 14, Vector2.zero, new Vector2(260, 30)).color = DIM;

        // ── Network metrics panel (top left, F3) ────────────────────────
        _metricsPanel = MakeBox(root, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(220, 90), new Vector2(8, -8), BG);
        _metricsText = Txt(_metricsPanel.transform, "RTT: --\nTick: --\nFPS: --\nServer: --",
            11, new Vector2(8, -8), new Vector2(204, 80));
        _metricsPanel.SetActive(false);

        // ── Top right: ping + players + minimap ─────────────────────────
        var trPanel = MakeBox(root, new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(160, 46), new Vector2(-8, -8), BG);
        var trT = trPanel.transform;
        _playersText = Txt(trT, "Players: 0/4", 11, new Vector2(8, -8),  new Vector2(144, 18));
        _pingText    = Txt(trT, "Ping: -- ms",  11, new Vector2(8, -26), new Vector2(144, 18));

        // Minimap
        var mapBox = MakeBox(root, new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(160, 160), new Vector2(-8, -62), BG);
        var mapInner = new GameObject("MinimapImage");
        mapInner.transform.SetParent(mapBox.transform, false);
        var mrt = mapInner.AddComponent<RectTransform>();
        mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0.5f);
        mrt.sizeDelta = new Vector2(148, 148);
        mrt.anchoredPosition = Vector2.zero;
        _minimapImage = mapInner.AddComponent<RawImage>();
        _minimapImage.texture = _minimapTex;

        var compassLabel = TxtCenter(mapBox.transform, "N", 12,
            new Vector2(0, 68), new Vector2(20, 20));
        compassLabel.color = DIM;

        // ── Bottom left: stat bars ──────────────────────────────────────
        var statsPanel = MakeBox(root, new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(210, 86), new Vector2(8, 8), BG);
        float barY = -8f;
        MakeStatBar(statsPanel.transform, "♥ HP",      HP_COL, ref barY, 100, 100, out _hpBar,      out _hpVal);
        MakeStatBar(statsPanel.transform, "⚡ Stamina", ST_COL, ref barY, 100, 100, out _staminaBar, out _staminaVal);
        MakeStatBar(statsPanel.transform, "🛡 Armor",   AR_COL, ref barY,  50,  50, out _armorBar,   out _armorVal);

        // ── Bottom center: hotbar (3 slots) ─────────────────────────────
        var hotbarPanel = MakeBox(root, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(226, 70), new Vector2(0, 8), BG);
        for (int i = 0; i < 3; i++)
            MakeHotbarSlot(hotbarPanel.transform, i);

        // ── Bottom right: item slots (4) ─────────────────────────────────
        var itemPanel = MakeBox(root, new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(230, 70), new Vector2(-8, 8), BG);
        for (int i = 0; i < 4; i++)
            MakeItemSlot(itemPanel.transform, i);
    }

    void MakeStatBar(Transform parent, string label, Color barCol, ref float y,
        float cur, float max, out Image bar, out TMP_Text val)
    {
        // Label
        var lbl = Txt(parent, label, 10, new Vector2(8, y), new Vector2(60, 18));
        lbl.color = DIM;

        // Value
        val = Txt(parent, $"{cur}/{max}", 10, new Vector2(148, y), new Vector2(56, 18));
        val.alignment = TextAlignmentOptions.Right;

        // Bar background
        var bgGo = MakeBox(parent, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(130, 10), new Vector2(8, y - 14), new Color(0.1f, 0.1f, 0.1f, 0.8f));

        // Bar fill
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        var frt = fillGo.AddComponent<RectTransform>();
        frt.anchorMin = new Vector2(0, 0);
        frt.anchorMax = new Vector2(1, 1);
        frt.sizeDelta = Vector2.zero;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        bar = fillGo.AddComponent<Image>();
        bar.color = barCol;
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillAmount = max > 0 ? cur / max : 0;

        y -= 26f;
    }

    void MakeHotbarSlot(Transform parent, int index)
    {
        float x = 8 + index * (62 + 4);
        var slot = MakeBox(parent, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(62, 58), new Vector2(x, -6), new Color(0.08f, 0.11f, 0.17f, 1f));

        // Slot number
        var num = Txt(slot.transform, (index + 1).ToString(), 9,
            new Vector2(4, -4), new Vector2(14, 14));
        num.color = DIM;
    }

    void MakeItemSlot(Transform parent, int index)
    {
        float x = 8 + index * (52 + 4);
        var slot = MakeBox(parent, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(52, 58), new Vector2(x, -6), new Color(0.08f, 0.11f, 0.17f, 1f));

        // Slot number
        var num = Txt(slot.transform, (index + 1).ToString(), 9,
            new Vector2(3, -3), new Vector2(14, 14));
        num.color = DIM;
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

    GameObject MakeBox(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject("Box");
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

    TMP_Text Txt(Transform parent, string text, int size, Vector2 pos, Vector2 size2)
    {
        var go = new GameObject(text.Length > 20 ? text[..20] : text);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = size2;
        rt.anchoredPosition = pos;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = Color.white;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

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
