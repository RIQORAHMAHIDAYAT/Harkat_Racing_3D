using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MinimapController - Dynamic top-down minimap system.
/// Attach to the player car. All UI and camera objects are created at runtime
/// so this script does NOT touch any existing scene objects.
/// </summary>
public class MinimapController : MonoBehaviour
{
    // ========================================
    // SETTINGS  (konstanta — tidak bergantung pada nilai Inspector)
    // ========================================
    private const float CAM_HEIGHT       = 90f;   // Ketinggian kamera di atas mobil
    private const float ORTHO_SIZE       = 55f;   // Radius pandangan peta
    private const float CAM_FOLLOW_SPEED = 12f;   // Smooth-follow speed
    private const float PANEL_SIZE       = 130f;  // Ukuran panel minimap dalam pixel
    private const float EDGE_PADDING     = 5f;    // Jarak dari tepi layar (px)
    private const int   TEX_RESOLUTION   = 256;   // Resolusi render texture

    // ========================================
    // INTERNAL REFERENCES
    // ========================================
    private Camera         _minimapCam;
    private RenderTexture  _renderTex;
    private RectTransform  _indicatorRect; // Rotasi indikator panah mengikuti arah mobil

    // ========================================
    // STARTUP
    // ========================================
    private void Start()
    {
        _renderTex = CreateRenderTexture();
        CreateMinimapCamera(_renderTex);
        Canvas canvas = GetOrCreateCanvas();
        BuildMinimapUI(canvas, _renderTex);
    }

    // ========================================
    // RUNTIME UPDATE
    // ========================================
    private void LateUpdate()
    {
        // Kamera minimap mengikuti posisi mobil (X,Z) dengan smooth lerp
        if (_minimapCam != null)
        {
            Vector3 target = new Vector3(transform.position.x,
                                         transform.position.y + CAM_HEIGHT,
                                         transform.position.z);
            _minimapCam.transform.position = Vector3.Lerp(
                _minimapCam.transform.position, target,
                Time.deltaTime * CAM_FOLLOW_SPEED);
        }

        // Rotasi indikator panah sesuai arah hadap mobil (hanya sumbu Y)
        if (_indicatorRect != null)
        {
            float yaw = transform.eulerAngles.y;
            _indicatorRect.localRotation = Quaternion.Euler(0f, 0f, -yaw);
        }
    }

    // ========================================
    // CLEANUP
    // ========================================
    private void OnDestroy()
    {
        if (_renderTex != null) { _renderTex.Release(); Destroy(_renderTex); }
        if (_minimapCam != null) Destroy(_minimapCam.gameObject);
    }

    // ========================================
    // FACTORY METHODS
    // ========================================

    private RenderTexture CreateRenderTexture()
    {
        RenderTexture rt = new RenderTexture(TEX_RESOLUTION, TEX_RESOLUTION, 24,
                                             RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        rt.antiAliasing = 2;
        rt.Create();
        return rt;
    }

    private void CreateMinimapCamera(RenderTexture rt)
    {
        GameObject go = new GameObject("Minimap_Camera");
        // Rotasi 90 derajat ke bawah (orthographic top-down), orientasi tetap North
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        _minimapCam                    = go.AddComponent<Camera>();
        _minimapCam.orthographic       = true;
        _minimapCam.orthographicSize   = ORTHO_SIZE;
        _minimapCam.clearFlags         = CameraClearFlags.SolidColor;
        _minimapCam.backgroundColor    = new Color(0.08f, 0.08f, 0.10f, 1f); // Dark background
        _minimapCam.targetTexture      = rt;
        _minimapCam.depth              = -2;        // Render lebih awal dari main camera
        _minimapCam.nearClipPlane      = 0.1f;
        _minimapCam.farClipPlane       = 500f;
        // Jangan render UI layer di minimap supaya widget UI pemain tidak muncul di peta
        _minimapCam.cullingMask        = ~LayerMask.GetMask("UI");
        // Jangan render ke layar utama, hanya ke texture
        _minimapCam.rect               = new Rect(0, 0, 1, 1);
    }

    private Canvas GetOrCreateCanvas()
    {
        // Cari canvas yang sudah ada dengan mode overlay
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;
        }

        // Buat baru jika tidak ada
        GameObject go  = new GameObject("Minimap_Canvas");
        Canvas canvas  = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Di atas UI lain

        // ConstantPixelSize = ukuran panel SELALU tepat sesuai nilai pixel yang kita set,
        // tidak tergantung resolusi layar. Ini yang benar untuk elemen HUD berukuran tetap.
        CanvasScaler scaler            = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode             = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor             = 1f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private void BuildMinimapUI(Canvas canvas, RenderTexture rt)
    {
        // -------------------------------------------------------
        // ROOT PANEL — Posisi pojok KIRI ATAS yang benar-benar pas
        // -------------------------------------------------------
        // Kita pakai anchor + pivot = (0, 1) dan anchoredPosition = (padding, -padding)
        // Dengan referenceResolution 1920x1080 ini selalu akurat di pojok kiri atas.
        GameObject panelGO       = new GameObject("Minimap_Panel");
        panelGO.transform.SetParent(canvas.transform, false);

        RectTransform panelRect  = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin      = new Vector2(0f, 1f);
        panelRect.anchorMax      = new Vector2(0f, 1f);
        panelRect.pivot          = new Vector2(0f, 1f);
        // Offset: +edgePadding dari kiri, -edgePadding dari atas
        panelRect.anchoredPosition = new Vector2(EDGE_PADDING, -EDGE_PADDING);
        panelRect.sizeDelta      = new Vector2(PANEL_SIZE, PANEL_SIZE);

        // Background gelap dengan sudut membulat (gradient ring effect)
        Image bgImage = panelGO.AddComponent<Image>();
        bgImage.color = new Color(0.04f, 0.04f, 0.06f, 0.88f);

        // -------------------------------------------------------
        // OUTER RING BORDER  — efek glossy ring premium
        // -------------------------------------------------------
        GameObject borderGO      = CreateRingBorder(panelGO, PANEL_SIZE);

        // -------------------------------------------------------
        // MAP DISPLAY (RawImage) — sedikit inset dari border
        // -------------------------------------------------------
        float mapDisplaySize     = PANEL_SIZE - 10f; // Inset 5px tiap sisi
        GameObject mapGO         = new GameObject("Minimap_Map");
        mapGO.transform.SetParent(panelGO.transform, false);

        RectTransform mapRect    = mapGO.AddComponent<RectTransform>();
        mapRect.anchorMin        = new Vector2(0.5f, 0.5f);
        mapRect.anchorMax        = new Vector2(0.5f, 0.5f);
        mapRect.pivot            = new Vector2(0.5f, 0.5f);
        mapRect.anchoredPosition = Vector2.zero;
        mapRect.sizeDelta        = new Vector2(mapDisplaySize, mapDisplaySize);

        RawImage rawImage        = mapGO.AddComponent<RawImage>();
        rawImage.texture         = rt;
        rawImage.color           = Color.white;

        // -------------------------------------------------------
        // PLAYER INDICATOR  — Segitiga panah menunjuk arah hadap
        // -------------------------------------------------------
        _indicatorRect           = CreateArrowIndicator(panelGO);

        // -------------------------------------------------------
        // CORNER LABEL "MAP"
        // -------------------------------------------------------
        CreateMapLabel(panelGO, PANEL_SIZE);
    }

    // ----- Helper: Outer ring border -----
    private GameObject CreateRingBorder(GameObject parent, float size)
    {
        GameObject go          = new GameObject("Minimap_Border");
        go.transform.SetParent(parent.transform, false);

        RectTransform rt       = go.AddComponent<RectTransform>();
        rt.anchorMin           = Vector2.zero;
        rt.anchorMax           = Vector2.one;
        rt.offsetMin           = Vector2.zero;
        rt.offsetMax           = Vector2.zero;

        Image img              = go.AddComponent<Image>();
        // Warna emas/amber untuk border premium seperti HUD balap
        img.color              = new Color(1f, 0.78f, 0.1f, 0.85f);

        // Kita buat frame sederhana dengan Outline effect agar tidak memerlukan sprite
        Outline outline        = go.AddComponent<Outline>();
        outline.effectColor    = new Color(1f, 0.78f, 0.1f, 0.4f);
        outline.effectDistance = new Vector2(2f, 2f);

        // Buat frame transparan (hanya border yang terlihat melalui outline)
        img.color              = new Color(1f, 0.78f, 0.1f, 0.0f); // Transparan tengah
        // Tambahkan Shadow agar border terlihat glowing
        Shadow shadow          = go.AddComponent<Shadow>();
        shadow.effectColor     = new Color(1f, 0.78f, 0.1f, 0.6f);
        shadow.effectDistance  = new Vector2(0f, 0f);

        return go;
    }

    // ----- Helper: Procedural arrow indicator -----
    private RectTransform CreateArrowIndicator(GameObject parent)
    {
        // Container untuk rotasi — supaya indikator berputar di tengah panel
        GameObject container          = new GameObject("Minimap_Indicator_Root");
        container.transform.SetParent(parent.transform, false);

        RectTransform containerRect   = container.AddComponent<RectTransform>();
        containerRect.anchorMin       = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax       = new Vector2(0.5f, 0.5f);
        containerRect.pivot           = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta       = new Vector2(20f, 20f);

        // Glow outer (bayangan putih halus)
        GameObject glowGO             = new GameObject("Indicator_Glow");
        glowGO.transform.SetParent(container.transform, false);

        RectTransform glowRect        = glowGO.AddComponent<RectTransform>();
        glowRect.anchorMin            = new Vector2(0.5f, 0.5f);
        glowRect.anchorMax            = new Vector2(0.5f, 0.5f);
        glowRect.pivot                = new Vector2(0.5f, 0.5f);
        glowRect.anchoredPosition     = Vector2.zero;
        glowRect.sizeDelta            = new Vector2(22f, 22f);

        Image glowImage               = glowGO.AddComponent<Image>();
        glowImage.color               = new Color(1f, 1f, 1f, 0.3f);

        Shadow glowShadow             = glowGO.AddComponent<Shadow>();
        glowShadow.effectColor        = new Color(1f, 1f, 0.4f, 0.8f);
        glowShadow.effectDistance     = new Vector2(0f, -1f);

        // Ikon panah (segitiga yang direpresentasikan oleh canvas UI)
        // Kita buat segitiga dari 3 layer persegi panjang yang dirotasi — teknik umum tanpa sprite
        BuildArrowShape(container, new Color(1f, 0.92f, 0.2f, 1f)); // Warna kuning/amber

        return containerRect;
    }

    // ----- Membangun bentuk panah dari Image UI -----
    // Teknik: Dua persegi panjang tipis yang dirotasi ±45 derajat membentuk "V" panah ke atas
    private void BuildArrowShape(GameObject parent, Color color)
    {
        // Titik depan panah (body atas - tegak)
        CreateArrowPart(parent, "Arrow_Body",
            anchoredPos : new Vector2(0f, 2f),
            size        : new Vector2(8f, 14f),
            rotation    : 0f,
            color       : color);

        // Sirip kiri panah
        CreateArrowPart(parent, "Arrow_WingL",
            anchoredPos : new Vector2(-5.5f, -4f),
            size        : new Vector2(5f, 12f),
            rotation    : 35f,
            color       : color);

        // Sirip kanan panah
        CreateArrowPart(parent, "Arrow_WingR",
            anchoredPos : new Vector2(5.5f, -4f),
            size        : new Vector2(5f, 12f),
            rotation    : -35f,
            color       : color);

        // Outline hitam tipis di belakang agar terbaca di atas track terang
        CreateArrowPart(parent, "Arrow_OutlineBody",
            anchoredPos : new Vector2(0f, 2f),
            size        : new Vector2(10f, 16f),
            rotation    : 0f,
            color       : new Color(0f, 0f, 0f, 0.55f));

        // Pastikan outline di bawah layer warna
        parent.transform.Find("Arrow_OutlineBody").SetAsFirstSibling();
    }

    private void CreateArrowPart(GameObject parent, string name,
                                  Vector2 anchoredPos, Vector2 size,
                                  float rotation, Color color)
    {
        GameObject go          = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        RectTransform rt       = go.AddComponent<RectTransform>();
        rt.anchorMin           = new Vector2(0.5f, 0.5f);
        rt.anchorMax           = new Vector2(0.5f, 0.5f);
        rt.pivot               = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition    = anchoredPos;
        rt.sizeDelta           = size;
        rt.localRotation       = Quaternion.Euler(0f, 0f, rotation);

        Image img              = go.AddComponent<Image>();
        img.color              = color;
    }

    // ----- Helper: Label "MAP" di sudut bawah -----
    private void CreateMapLabel(GameObject parent, float size)
    {
        // Menggunakan Text biasa agar tidak butuh TextMeshPro dependency
        GameObject go          = new GameObject("Minimap_Label");
        go.transform.SetParent(parent.transform, false);

        RectTransform rt       = go.AddComponent<RectTransform>();
        rt.anchorMin           = new Vector2(0f, 0f);
        rt.anchorMax           = new Vector2(1f, 0f);
        rt.pivot               = new Vector2(0.5f, 0f);
        rt.anchoredPosition    = new Vector2(0f, 4f);
        rt.sizeDelta           = new Vector2(0f, 16f);

        Text label             = go.AddComponent<Text>();
        label.text             = "MINIMAP";
        label.alignment        = TextAnchor.MiddleCenter;
        label.fontSize         = 9;
        label.fontStyle        = FontStyle.Bold;
        label.color            = new Color(1f, 0.78f, 0.1f, 0.85f);

        Shadow shadow          = go.AddComponent<Shadow>();
        shadow.effectColor     = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance  = new Vector2(1f, -1f);
    }
}
