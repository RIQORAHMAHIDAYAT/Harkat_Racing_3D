using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GameHUD — Singleton yang mengontrol semua UI dalam game:
///   • Coin counter (atas tengah)
///   • Race timer (atas tengah)
///   • Hint "Tekan W" saat menunggu start
///   • Countdown 3-2-1-GO! animasi
///   • Boost indicator saat booster aktif
///   • Tombol Pause (pojok kiri atas)
///   • Pause panel (mirip Settings Main Menu + tombol Back to Home)
///
/// CARA PASANG:
///   1. Buat GameObject kosong di scene game (misal: "GameHUD")
///   2. Drag script ini ke GameObject tersebut
///   3. Isi field "Main Menu Scene Name" di Inspector
///   4. Isi "Total Coins" sesuai jumlah coin di scene
///   5. Semua UI dibuat otomatis saat runtime — tidak perlu setup Canvas manual
/// </summary>
public class GameHUD : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // SINGLETON
    // ─────────────────────────────────────────────────────────────────
    public static GameHUD Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────
    // INSPECTOR SETTINGS
    // ─────────────────────────────────────────────────────────────────
    [Header("Scene Configuration")]
    [Tooltip("Nama scene Main Menu untuk tombol Back to Home")]
    public string mainMenuSceneName = "MainMenu";

    [Tooltip("Total coin yang ada di scene (untuk tampilan X/Y)")]
    public int totalCoins = 5;

    // ─────────────────────────────────────────────────────────────────
    // RACE STATE
    // ─────────────────────────────────────────────────────────────────
    private enum RaceState { WaitingForStart, Countdown, Racing, Paused, Finished }
    private RaceState _state = RaceState.WaitingForStart;

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE DATA
    // ─────────────────────────────────────────────────────────────────
    private mobil   _car;
    private int     _coinsCollected = 0;
    private float   _raceTime       = 0f;

    // ─────────────────────────────────────────────────────────────────
    // UI REFERENCES
    // ─────────────────────────────────────────────────────────────────
    private Canvas              _canvas;
    private TextMeshProUGUI     _coinText;
    private TextMeshProUGUI     _timerText;
    private GameObject          _pressWHint;
    private GameObject          _countdownOverlay;
    private TextMeshProUGUI     _countdownNumber;
    private GameObject          _boostIndicator;
    private CanvasGroup         _boostCG;
    private TextMeshProUGUI     _boostTimerText;
    private Button              _pauseBtn;
    private GameObject          _pausePanel;
    private GameObject          _finishPanel;   // Panel tampil saat balapan selesai
    private TextMeshProUGUI     _finishTimeText; // Teks waktu final di finish panel
    private TextMeshProUGUI     _finishCoinText; // Teks coin final di finish panel

    // Settings toggle dalam pause panel
    private Image               _musicBg;
    private TextMeshProUGUI     _musicTxt;
    private Image               _soundBg;
    private TextMeshProUGUI     _soundTxt;
    private bool                _musicOn = true;
    private bool                _soundOn = true;

    // Control mode (mirip SettingsManager di Main Menu)
    private TextMeshProUGUI     _controlTxt;
    private int                 _controlIndex = 0;
    private readonly string[]   _controlModes = { "Keyboard", "Gamepad", "Mobile" };
    private static readonly Color ColCtrlBlue = new Color(0.22f, 0.50f, 0.87f, 1f);

    // ─────────────────────────────────────────────────────────────────
    // COLORS (Tema racing — dark premium)
    // ─────────────────────────────────────────────────────────────────
    private static readonly Color ColGold    = new Color(1.00f, 0.84f, 0.00f, 1f);
    private static readonly Color ColCyan    = new Color(0.15f, 0.88f, 1.00f, 1f);
    private static readonly Color ColGreen   = new Color(0.18f, 0.78f, 0.32f, 1f);
    private static readonly Color ColRed     = new Color(0.82f, 0.18f, 0.18f, 1f);
    private static readonly Color ColDarkBg  = new Color(0.04f, 0.04f, 0.12f, 0.88f);
    private static readonly Color ColPanel   = new Color(0.06f, 0.07f, 0.16f, 0.97f);
    private static readonly Color ColOverlay = new Color(0.00f, 0.00f, 0.08f, 0.74f);
    private static readonly Color ColBlue    = new Color(0.08f, 0.45f, 0.98f, 0.92f);
    private static readonly Color ColDivider = new Color(0.25f, 0.30f, 0.55f, 0.65f);

    // ═════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════════════════════════
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Pastikan EventSystem ada — wajib agar tombol UI bisa diklik
        EnsureEventSystem();

        // Cari mobil pemain dan kunci input saat menunggu start
        _car = FindObjectOfType<mobil>();
        if (_car != null)
            _car.SetInputLocked(true);

        // Cek apakah ada FinishLine di scene, jika tidak ada, buat otomatis
        if (FindObjectOfType<FinishLine>() == null)
        {
            Debug.Log("[GameHUD] FinishLine tidak ditemukan! Membuat otomatis di posisi mobil...");
            if (_car != null)
            {
                GameObject finishObj = new GameObject("FinishLine_Auto");
                // Posisikan tepat di mobil. Karena di FinishLine.cs kita sudah set 
                // crossing pertama di awal balapan diabaikan (minTimeBetweenCross = 5s), 
                // ini akan menjadi garis finish yang sempurna setelah 1 putaran.
                finishObj.transform.position = _car.transform.position;
                finishObj.transform.rotation = _car.transform.rotation;
                
                BoxCollider col = finishObj.AddComponent<BoxCollider>();
                col.isTrigger = true;
                // Buat sangat lebar dan tinggi agar tidak terlewat
                col.size = new Vector3(80f, 20f, 5f); 
                
                finishObj.AddComponent<FinishLine>();
            }
        }

        // Bangun seluruh UI secara runtime
        _canvas = CreateCanvas();
        BuildHUD();

        // Inisialisasi tampilan awal
        RefreshCoin();
        RefreshTimer();
    }

    private void Update()
    {
        switch (_state)
        {
            // ─ Menunggu pemain tekan W ─────────────────────
            case RaceState.WaitingForStart:
                if (Input.GetKeyDown(KeyCode.W)       ||
                    Input.GetKeyDown(KeyCode.UpArrow)  ||
                    Input.GetKeyDown(KeyCode.S)        || // juga S (mundur)
                    Input.GetKeyDown(KeyCode.DownArrow))
                {
                    StartCountdown();
                }
                break;

            // ─ Balapan berjalan: update timer ─────────────
            case RaceState.Racing:
                _raceTime += Time.deltaTime;
                RefreshTimer();
                break;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // PUBLIC API  (dipanggil dari CoinPickup / BoosterPickup)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>Tambah 1 coin ke counter. Dipanggil CoinPickup.</summary>
    public void AddCoin()
    {
        _coinsCollected++;
        RefreshCoin();
        // Animasi "pop" kecil pada teks coin
        StartCoroutine(PunchScale(_coinText.rectTransform, 1.45f, 0.22f));
    }

    /// <summary>Tampilkan indikator boost aktif. Dipanggil BoosterPickup.</summary>
    public void ShowBoostActivated(float duration = 3f)
    {
        StartCoroutine(FlashBoostIndicator(duration));
    }

    /// <summary>Dipanggil FinishLine saat mobil melewati garis finis.</summary>
    public void TriggerFinish()
    {
        if (_state != RaceState.Racing) return;
        _state = RaceState.Finished;
        if (_car != null) _car.SetInputLocked(true);

        // Isi teks waktu final di finish panel
        if (_finishTimeText != null)
            _finishTimeText.text = FormatTime(_raceTime);

        // Isi teks coin yang dikumpulkan
        if (_finishCoinText != null)
            _finishCoinText.text = $"{_coinsCollected} / {totalCoins}";

        // Tampilkan layar finish
        if (_finishPanel != null) _finishPanel.SetActive(true);
        if (_pauseBtn   != null) _pauseBtn.gameObject.SetActive(false);
        Debug.Log($"[GameHUD] Balapan selesai! Waktu: {FormatTime(_raceTime)}");
    }

    // ═════════════════════════════════════════════════════════════════
    // COUNTDOWN
    // ═════════════════════════════════════════════════════════════════
    private void StartCountdown()
    {
        _state = RaceState.Countdown;
        if (_pressWHint != null) _pressWHint.SetActive(false);
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        _countdownOverlay.SetActive(true);

        // Data tiap step countdown
        string[] labels = { "3",     "2",     "1",     "GO!" };
        Color[]  colors =
        {
            new Color(1.0f, 0.30f, 0.12f, 1f), // Merah — 3
            new Color(1.0f, 0.82f, 0.04f, 1f), // Kuning — 2
            new Color(0.2f, 0.88f, 0.28f, 1f), // Hijau — 1
            new Color(0.15f, 0.88f, 1.0f, 1f), // Cyan — GO!
        };
        float[] holdSec = { 0.62f, 0.62f, 0.62f, 0.75f };

        for (int i = 0; i < labels.Length; i++)
        {
            _countdownNumber.text  = labels[i];
            _countdownNumber.color = colors[i];
            _countdownNumber.transform.localScale = Vector3.one * 0.35f;

            // ── Scale-in ─────────────────────────────────────
            float dur  = 0.20f;
            float peak = (i == labels.Length - 1) ? 1.10f : 1.20f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float s = Mathf.SmoothStep(0.35f, peak, t / dur);
                _countdownNumber.transform.localScale = Vector3.one * s;
                yield return null;
            }
            _countdownNumber.transform.localScale = Vector3.one * peak;

            // ── Hold ─────────────────────────────────────────
            float wait = holdSec[i] - dur;
            if (wait > 0f) yield return new WaitForSeconds(wait);

            // ── Fade-out ─────────────────────────────────────
            float fadeDur = 0.15f;
            Color startCol = colors[i];
            for (float t = 0f; t < fadeDur; t += Time.deltaTime)
            {
                float a = Mathf.Lerp(1f, 0f, t / fadeDur);
                _countdownNumber.color = new Color(startCol.r, startCol.g, startCol.b, a);
                yield return null;
            }
        }

        _countdownOverlay.SetActive(false);

        // Mulai balapan!
        if (_car != null) _car.SetInputLocked(false);
        _state = RaceState.Racing;
    }

    // ═════════════════════════════════════════════════════════════════
    // BOOST INDICATOR
    // ═════════════════════════════════════════════════════════════════
    private IEnumerator FlashBoostIndicator(float boostDuration)
    {
        _boostIndicator.SetActive(true);
        _boostCG.alpha = 0f;

        // Fade in
        for (float t = 0f; t < 1f; t += Time.deltaTime * 5f)
        {
            _boostCG.alpha = Mathf.Clamp01(t);
            yield return null;
        }
        _boostCG.alpha = 1f;

        // Countdown timer teks boost ("3 ... 2 ... 1")
        float remaining = boostDuration;
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            if (_boostTimerText != null)
                _boostTimerText.text = $"⚡  SPEED BOOST  {Mathf.CeilToInt(remaining)}s  ⚡";
            yield return null;
        }

        // Fade out
        for (float t = 1f; t > 0f; t -= Time.deltaTime * 2.5f)
        {
            _boostCG.alpha = Mathf.Clamp01(t);
            yield return null;
        }
        _boostCG.alpha = 0f;
        _boostIndicator.SetActive(false);
    }

    // ═════════════════════════════════════════════════════════════════
    // PAUSE / RESUME / HOME
    // ═════════════════════════════════════════════════════════════════
    private void TogglePause()
    {
        if (_state == RaceState.Paused) ResumeGame();
        else if (_state == RaceState.Racing) PauseGame();
    }

    private void PauseGame()
    {
        _state = RaceState.Paused;
        Time.timeScale = 0f;
        if (_car != null) _car.SetInputLocked(true);

        _pausePanel.SetActive(true);
        if (_pauseBtn != null) _pauseBtn.gameObject.SetActive(false);
    }

    private void ResumeGame()
    {
        _state = RaceState.Racing;
        Time.timeScale = 1f;
        if (_car != null) _car.SetInputLocked(false);

        _pausePanel.SetActive(false);
        if (_pauseBtn != null) _pauseBtn.gameObject.SetActive(true);
    }

    private void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ─── Settings toggle di pause panel ─────────────────────────────
    private void ToggleMusic()
    {
        _musicOn       = !_musicOn;
        _musicBg.color = _musicOn ? ColGreen : ColRed;
        _musicTxt.text = _musicOn ? "ON" : "OFF";
        // TODO: AudioManager.Instance?.SetMusicEnabled(_musicOn);
    }

    private void ToggleSound()
    {
        _soundOn       = !_soundOn;
        _soundBg.color = _soundOn ? ColGreen : ColRed;
        _soundTxt.text = _soundOn ? "ON" : "OFF";
        // TODO: AudioManager.Instance?.SetSoundEnabled(_soundOn);
    }

    private void NextControlMode()
    {
        _controlIndex = (_controlIndex + 1) % _controlModes.Length;
        if (_controlTxt != null) _controlTxt.text = _controlModes[_controlIndex];
    }

    // ═════════════════════════════════════════════════════════════════
    // DATA REFRESH
    // ═════════════════════════════════════════════════════════════════
    private void RefreshCoin()
    {
        if (_coinText != null)
            _coinText.text = $"COIN  {_coinsCollected} / {totalCoins}";
    }

    private void RefreshTimer()
    {
        if (_timerText == null) return;
        _timerText.text = FormatTime(_raceTime);
    }

    private string FormatTime(float t)
    {
        int mins = (int)(t / 60f);
        int secs = (int)(t % 60f);
        int ms   = (int)((t % 1f) * 100f);
        return $"{mins:00}:{secs:00}<size=65%>.{ms:00}</size>";
    }

    // ═════════════════════════════════════════════════════════════════
    // ANIMATION HELPERS
    // ═════════════════════════════════════════════════════════════════
    private IEnumerator PunchScale(RectTransform rt, float peak, float duration)
    {
        float half = duration * 0.5f;
        for (float t = 0f; t < half; t += Time.deltaTime)
        { rt.localScale = Vector3.one * Mathf.Lerp(1f, peak, t / half); yield return null; }
        for (float t = 0f; t < half; t += Time.deltaTime)
        { rt.localScale = Vector3.one * Mathf.Lerp(peak, 1f, t / half); yield return null; }
        rt.localScale = Vector3.one;
    }

    private IEnumerator PulseAlpha(TextMeshProUGUI tmp, Color colA, Color colB, float speed)
    {
        float t = 0f;
        while (tmp != null && tmp.gameObject.activeInHierarchy)
        {
            t += Time.deltaTime * speed;
            tmp.color = Color.Lerp(colA, colB, (Mathf.Sin(t) + 1f) * 0.5f);
            yield return null;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // HUD BUILDER — Main entry
    // ═════════════════════════════════════════════════════════════════
    private void BuildHUD()
    {
        BuildTopBar();          // Coin + Timer di atas tengah
        BuildPressWHint();      // "Tekan W untuk memulai"
        BuildPauseButton();     // Tombol ❚❚ pojok KANAN atas
        BuildBoostIndicator();  // Indikator boost tengah layar
        BuildCountdownOverlay();// Overlay countdown — bangun setelah lainnya agar di layer atas
        BuildPausePanel();      // Pause panel — paling atas
        BuildFinishPanel();     // Layar finish — paling atas
    }

    // ── Top Bar ──────────────────────────────────────────────────────
    private void BuildTopBar()
    {
        GameObject bar = MakePanel("HUD_TopBar", _canvas.transform,
            pivot: V2(0.5f, 1f),
            anMin: V2(0.5f, 1f), anMax: V2(0.5f, 1f),
            size:  V2(290f, 74f), pos: V2(0f, -10f));
        SetImg(bar, ColDarkBg);

        // Garis aksen bawah panel (cyan tipis)
        GameObject accent = MakePanel("TopBar_Accent", bar.transform,
            pivot: V2(0.5f, 0f),
            anMin: V2(0f, 0f), anMax: V2(1f, 0f),
            size:  V2(0f, 2f), pos: V2(0f, 0f));
        accent.GetComponent<RectTransform>().offsetMin = V2(0, 0);
        accent.GetComponent<RectTransform>().offsetMax = V2(0, 2);
        SetImg(accent, ColCyan);

        // ─ Coin text (atas) ──────────────────────────────
        _coinText = MakeTMP("HUD_Coin", bar.transform, $"COIN  0 / {totalCoins}", 22f, ColGold, TextAlignmentOptions.Center);
        Stretch(_coinText.rectTransform, V2(10f, 2f), V2(-10f, 0f), V2(0f, 0.5f), V2(1f, 1f));
        _coinText.fontStyle = FontStyles.Bold;

        // ─ Timer text (bawah) ────────────────────────────
        _timerText = MakeTMP("HUD_Timer", bar.transform, "00:00<size=65%>.00</size>", 30f, Color.white, TextAlignmentOptions.Center);
        Stretch(_timerText.rectTransform, V2(10f, 0f), V2(-10f, -2f), V2(0f, 0f), V2(1f, 0.52f));
        _timerText.fontStyle = FontStyles.Bold;
    }

    // ── "Tekan W" Hint ───────────────────────────────────────────────
    private void BuildPressWHint()
    {
        _pressWHint = new GameObject("HUD_PressWHint");
        _pressWHint.transform.SetParent(_canvas.transform, false);
        RectTransform rt = _pressWHint.AddComponent<RectTransform>();
        rt.anchorMin        = V2(0.5f, 0.28f);
        rt.anchorMax        = V2(0.5f, 0.28f);
        rt.pivot            = V2(0.5f, 0.5f);
        rt.sizeDelta        = V2(600f, 70f);
        rt.anchoredPosition = V2(0f, 0f);

        TextMeshProUGUI hint = _pressWHint.AddComponent<TextMeshProUGUI>();
        hint.text               = "Tekan  [ W ]  untuk memulai balapan!";
        hint.fontSize           = 30f;
        hint.color              = Color.white;
        hint.alignment          = TextAlignmentOptions.Center;
        hint.fontStyle          = FontStyles.Bold;
        hint.enableWordWrapping = false;

        // Efek pulse warna putih ↔ emas
        StartCoroutine(PulseAlpha(hint, Color.white, ColGold, 2.2f));
    }

    // ── Pause Button (Top Right) — Desain tombol pause game proper ──────
    private void BuildPauseButton()
    {
        // Outer glow/shadow effect (sedikit lebih besar, gelap)
        GameObject shadow = MakePanel("HUD_PauseBtn_Shadow", _canvas.transform,
            pivot: V2(1f, 1f),
            anMin: V2(1f, 1f), anMax: V2(1f, 1f),
            size:  V2(56f, 56f), pos: V2(-13f, -13f));
        SetImg(shadow, new Color(0f, 0f, 0f, 0.45f));

        // Tombol utama (biru gelap premium)
        GameObject go = MakePanel("HUD_PauseBtn", _canvas.transform,
            pivot: V2(1f, 1f),
            anMin: V2(1f, 1f), anMax: V2(1f, 1f),
            size:  V2(54f, 54f), pos: V2(-15f, -15f));

        // Warna tombol pause: biru gelap premium dengan border
        Color pauseBtnColor = new Color(0.10f, 0.14f, 0.28f, 0.97f);
        SetImg(go, pauseBtnColor);

        // Tambahkan border tipis cyan di atas (accent line)
        GameObject border = MakePanel("PauseBtn_Border", go.transform,
            pivot: V2(0.5f, 1f),
            anMin: V2(0f, 1f), anMax: V2(1f, 1f),
            size:  V2(0f, 2.5f), pos: V2(0f, 0f));
        border.GetComponent<RectTransform>().offsetMin = V2(0, 0);
        border.GetComponent<RectTransform>().offsetMax = V2(0, 2);
        SetImg(border, ColCyan);

        _pauseBtn = go.AddComponent<Button>();
        Color pauseHover = new Color(0.20f, 0.30f, 0.55f, 1f);
        SetBtnColors(_pauseBtn, pauseBtnColor, pauseHover);
        _pauseBtn.onClick.AddListener(TogglePause);

        // ── Dua bar pause (II) dibuat sebagai 2 kotak putih tebal, bukan teks ──
        // Bar kiri
        GameObject barL = MakePanel("PauseBar_L", go.transform,
            pivot: V2(0.5f, 0.5f),
            anMin: V2(0.5f, 0.5f), anMax: V2(0.5f, 0.5f),
            size:  V2(9f, 22f), pos: V2(-7f, 0f));
        SetImg(barL, Color.white);

        // Bar kanan
        GameObject barR = MakePanel("PauseBar_R", go.transform,
            pivot: V2(0.5f, 0.5f),
            anMin: V2(0.5f, 0.5f), anMax: V2(0.5f, 0.5f),
            size:  V2(9f, 22f), pos: V2(7f, 0f));
        SetImg(barR, Color.white);
    }

    // ── Boost Indicator (tengah layar) ───────────────────────────────
    private void BuildBoostIndicator()
    {
        GameObject go = MakePanel("HUD_Boost", _canvas.transform,
            pivot: V2(0.5f, 0.5f),
            anMin: V2(0.5f, 0.62f), anMax: V2(0.5f, 0.62f),
            size:  V2(310f, 52f), pos: V2(0f, 0f));
        SetImg(go, ColBlue);

        _boostCG = go.AddComponent<CanvasGroup>();
        _boostCG.alpha = 0f;

        _boostTimerText = MakeTMP("BoostLbl", go.transform,
            "⚡  SPEED BOOST  3s  ⚡", 21f, Color.white, TextAlignmentOptions.Center);
        Stretch(_boostTimerText.rectTransform, V2(6f, 0f), V2(-6f, 0f), V2(0,0), V2(1f,1f));
        _boostTimerText.fontStyle = FontStyles.Bold;

        _boostIndicator = go;
        go.SetActive(false);
    }

    // ── Countdown Overlay ────────────────────────────────────────────
    private void BuildCountdownOverlay()
    {
        _countdownOverlay = new GameObject("HUD_CountdownOverlay");
        _countdownOverlay.transform.SetParent(_canvas.transform, false);

        RectTransform rt = _countdownOverlay.AddComponent<RectTransform>();
        rt.anchorMin = V2(0f, 0f);
        rt.anchorMax = V2(1f, 1f);
        rt.offsetMin = V2(0f, 0f);
        rt.offsetMax = V2(0f, 0f);
        SetImg(_countdownOverlay, new Color(0f, 0f, 0f, 0.52f));

        // Angka besar di tengah layar
        _countdownNumber = MakeTMP("CDNumber", _countdownOverlay.transform,
            "3", 200f, Color.white, TextAlignmentOptions.Center);
        Stretch(_countdownNumber.rectTransform,
            V2(80f, 0f), V2(-80f, 0f),
            V2(0f, 0.22f), V2(1f, 0.78f));
        _countdownNumber.fontStyle = FontStyles.Bold;

        _countdownOverlay.SetActive(false);
    }

    // ── Pause Panel ──────────────────────────────────────────────────
    //
    //  Layout (mengikuti style settings panel di Main Menu):
    //  ┌──────────────────────────────────────┐
    //  │            — PAUSED —                │  ← header tipis
    //  │  Music              [ ON  ]          │
    //  │  Sound              [ ON  ]          │
    //  │  Control            [Keyboard]       │
    //  │        [ ▶ RESUME ]                  │  ← hijau
    //  │        [ BACK TO HOME ]              │  ← merah (seperti Close)
    //  └──────────────────────────────────────┘
    private void BuildPausePanel()
    {
        // Overlay gelap seluruh layar
        _pausePanel = new GameObject("HUD_PausePanel");
        _pausePanel.transform.SetParent(_canvas.transform, false);
        RectTransform oRt = _pausePanel.AddComponent<RectTransform>();
        oRt.anchorMin = V2(0f, 0f);
        oRt.anchorMax = V2(1f, 1f);
        oRt.offsetMin = V2(0f, 0f);
        oRt.offsetMax = V2(0f, 0f);
        SetImg(_pausePanel, ColOverlay);

        // Card — ukuran dan posisi mirip panel settings di Main Menu
        GameObject card = MakePanel("PauseCard", _pausePanel.transform,
            pivot: V2(0.5f, 0.5f),
            anMin: V2(0.5f, 0.5f), anMax: V2(0.5f, 0.5f),
            size:  V2(440f, 400f), pos: V2(0f, 0f));
        SetImg(card, new Color(0.07f, 0.07f, 0.12f, 0.93f));

        // ── Header "— PAUSED —" (tipis, tidak dominan) ───────────────
        var header = MakeTMP("PT_Header", card.transform, "— PAUSED —", 22f,
            new Color(0.65f, 0.72f, 0.85f, 1f), TextAlignmentOptions.Center);
        RectRect(header.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f),
            V2(380f, 38f), V2(0f, -22f));

        // ── Divider ──────────────────────────────────────────────────
        var div = MakePanel("PT_Div", card.transform,
            pivot: V2(0.5f, 1f),
            anMin: V2(0.5f, 1f), anMax: V2(0.5f, 1f),
            size:  V2(380f, 1f), pos: V2(0f, -62f));
        SetImg(div, ColDivider);

        // ── Row: Music ───────────────────────────────────────────────
        float rowY = -82f;
        BuildSettingRow(card.transform, "Music",   rowY, ToggleMusic,     ref _musicBg, ref _musicTxt);

        // ── Row: Sound ───────────────────────────────────────────────
        rowY -= 66f;
        BuildSettingRow(card.transform, "Sound",   rowY, ToggleSound,     ref _soundBg, ref _soundTxt);

        // ── Row: Control (sama persis dengan SettingsManager) ────────
        rowY -= 66f;
        BuildControlRow(card.transform, rowY);

        // ── Resume Button (hijau) ─────────────────────────────────────
        rowY -= 78f;
        BuildActionBtn(card.transform, "▶   Resume", ColGreen, rowY, ResumeGame);

        // ── Back to Home Button (merah — seperti tombol Close) ───────
        rowY -= 62f;
        BuildActionBtn(card.transform, "Back to Home", ColRed, rowY, GoToMainMenu);

        _pausePanel.SetActive(false);
    }

    // ── Finish Panel (tampil saat balapan selesai) ───────────────────
    private void BuildFinishPanel()
    {
        // Full-screen overlay
        _finishPanel = new GameObject("HUD_FinishPanel");
        _finishPanel.transform.SetParent(_canvas.transform, false);
        RectTransform fRt = _finishPanel.AddComponent<RectTransform>();
        fRt.anchorMin = V2(0f, 0f);
        fRt.anchorMax = V2(1f, 1f);
        fRt.offsetMin = V2(0f, 0f);
        fRt.offsetMax = V2(0f, 0f);
        SetImg(_finishPanel, new Color(0f, 0f, 0.05f, 0.80f));

        // Card tengah
        GameObject card = MakePanel("FinishCard", _finishPanel.transform,
            pivot: V2(0.5f, 0.5f),
            anMin: V2(0.5f, 0.5f), anMax: V2(0.5f, 0.5f),
            size:  V2(460f, 400f), pos: V2(0f, 0f));
        SetImg(card, new Color(0.06f, 0.07f, 0.15f, 0.97f));

        // ── Judul FINISH ──────────────────────────────────────────────
        var title = MakeTMP("FinishTitle", card.transform, "FINISH!", 58f,
            ColGold, TextAlignmentOptions.Center);
        RectRect(title.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f),
            V2(400f, 72f), V2(0f, -42f));
        title.fontStyle = FontStyles.Bold;

        // ── Teks "Waktu Balapan" ─────────────────────────────────────────
        var lblTime = MakeTMP("FinishLabelTime", card.transform, "Waktu Balapan", 20f,
            new Color(0.7f, 0.78f, 0.9f, 1f), TextAlignmentOptions.Center);
        RectRect(lblTime.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f),
            V2(380f, 32f), V2(0f, -105f));

        // ── Timer final (diisi saat finish dipanggil) ─────────────────
        var finalTime = MakeTMP("FinishTime", card.transform, "00:00.00", 44f,
            ColCyan, TextAlignmentOptions.Center);
        RectRect(finalTime.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f),
            V2(380f, 56f), V2(0f, -145f));
        finalTime.fontStyle = FontStyles.Bold;
        _finishTimeText = finalTime;

        // ── Teks "Coin Didapat" ─────────────────────────────────────────
        var lblCoin = MakeTMP("FinishLabelCoin", card.transform, "Coin Emas Didapat", 20f,
            new Color(0.7f, 0.78f, 0.9f, 1f), TextAlignmentOptions.Center);
        RectRect(lblCoin.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f),
            V2(380f, 32f), V2(0f, -205f));

        // ── Coin final (diisi saat finish dipanggil) ─────────────────
        var finalCoin = MakeTMP("FinishCoin", card.transform, "0 / 5", 44f,
            ColGold, TextAlignmentOptions.Center);
        RectRect(finalCoin.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f),
            V2(380f, 56f), V2(0f, -245f));
        finalCoin.fontStyle = FontStyles.Bold;
        _finishCoinText = finalCoin;

        // ── Divider ───────────────────────────────────────────────────
        var div = MakePanel("FinishDiv", card.transform,
            pivot: V2(0.5f, 1f),
            anMin: V2(0.5f, 1f), anMax: V2(0.5f, 1f),
            size:  V2(380f, 1f), pos: V2(0f, -315f));
        SetImg(div, ColDivider);

        // ── Back to Main Menu (merah) ─────────────────────────────────
        BuildActionBtn(card.transform, "Back to Home", ColRed, -332f, GoToMainMenu);

        _finishPanel.SetActive(false);
    }


    // ─────────────────────────────────────────────────────────────────

    /// <summary>Baris toggle ON/OFF (Music & Sound) — sama gaya dengan settings Main Menu.</summary>
    private void BuildSettingRow(Transform parent, string label, float y,
        System.Action onToggle, ref Image outImg, ref TextMeshProUGUI outTxt)
    {
        // Label kiri
        var lbl = MakeTMP($"Row_{label}_Lbl", parent, label, 22f, Color.white, TextAlignmentOptions.Left);
        RectRect(lbl.rectTransform,
            V2(0f, 1f), V2(0f, 1f), V2(0f, 1f),
            V2(200f, 48f), V2(30f, y));

        // Tombol toggle kanan (hijau = ON, merah = OFF)
        GameObject btnGO = MakePanel($"Row_{label}_Btn", parent,
            pivot: V2(1f, 1f),
            anMin: V2(1f, 1f), anMax: V2(1f, 1f),
            size:  V2(108f, 40f), pos: V2(-30f, y - 4f));
        SetImg(btnGO, ColGreen);

        Button btn = btnGO.AddComponent<Button>();
        Image  img = btnGO.GetComponent<Image>();
        SetBtnColors(btn, ColGreen, new Color(0.28f, 0.92f, 0.48f, 1f));
        btn.onClick.AddListener(() => onToggle());

        var txt = MakeTMP($"Row_{label}_TxtBtn", btnGO.transform, "ON", 19f, Color.white, TextAlignmentOptions.Center);
        Stretch(txt.rectTransform, V2(0,0), V2(0,0), V2(0,0), V2(1f,1f));
        txt.fontStyle = FontStyles.Bold;

        outImg = img;
        outTxt = txt;
    }

    /// <summary>Baris Control mode (Keyboard/Gamepad/Mobile) — cycle button biru, sama dengan SettingsManager.</summary>
    private void BuildControlRow(Transform parent, float y)
    {
        // Label kiri
        var lbl = MakeTMP("Row_Control_Lbl", parent, "Control", 22f, Color.white, TextAlignmentOptions.Left);
        RectRect(lbl.rectTransform,
            V2(0f, 1f), V2(0f, 1f), V2(0f, 1f),
            V2(200f, 48f), V2(30f, y));

        // Tombol cycle biru (klik → ganti mode)
        GameObject btnGO = MakePanel("Row_Control_Btn", parent,
            pivot: V2(1f, 1f),
            anMin: V2(1f, 1f), anMax: V2(1f, 1f),
            size:  V2(108f, 40f), pos: V2(-30f, y - 4f));
        SetImg(btnGO, ColCtrlBlue);

        Button btn = btnGO.AddComponent<Button>();
        SetBtnColors(btn, ColCtrlBlue, new Color(0.35f, 0.65f, 1.0f, 1f));
        btn.onClick.AddListener(NextControlMode);

        _controlTxt = MakeTMP("Row_Control_TxtBtn", btnGO.transform, _controlModes[_controlIndex],
            19f, Color.white, TextAlignmentOptions.Center);
        Stretch(_controlTxt.rectTransform, V2(0,0), V2(0,0), V2(0,0), V2(1f,1f));
        _controlTxt.fontStyle = FontStyles.Bold;
    }

    private void BuildActionBtn(Transform parent, string label, Color col, float y, System.Action onClick)
    {
        GameObject go = MakePanel($"PBtn_{label}", parent,
            pivot: V2(0.5f, 1f),
            anMin: V2(0.5f, 1f), anMax: V2(0.5f, 1f),
            size:  V2(390f, 56f), pos: V2(0f, y));
        SetImg(go, col);

        Button btn = go.AddComponent<Button>();
        Color hi = new Color(
            Mathf.Min(col.r + 0.18f, 1f),
            Mathf.Min(col.g + 0.18f, 1f),
            Mathf.Min(col.b + 0.18f, 1f), 1f);
        SetBtnColors(btn, col, hi);
        btn.onClick.AddListener(() => onClick());

        var lbl = MakeTMP($"PBtn_{label}_T", go.transform, label, 22f, Color.white, TextAlignmentOptions.Center);
        Stretch(lbl.rectTransform, V2(8f, 0f), V2(-8f, 0f), V2(0,0), V2(1f,1f));
        lbl.fontStyle = FontStyles.Bold;
    }

    // ═════════════════════════════════════════════════════════════════
    // UI FACTORY HELPERS
    // ═════════════════════════════════════════════════════════════════
    private Canvas CreateCanvas()
    {
        // PENTING: Gunakan ConstantPixelSize (bukan ScaleWithScreenSize) agar tidak
        // mengacaukan ukuran minimap. MinimapController mencari Canvas ScreenSpaceOverlay
        // terdekat — jika kita pakai ScaleWithScreenSize maka minimap ikut ter-scale!
        GameObject go = new GameObject("GameHUDCanvas");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 50; // Di atas canvas lain (minimap, dll.)

        CanvasScaler sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // Sama dengan MinimapController
        sc.scaleFactor = 1f;

        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    /// <summary>
    /// Pastikan EventSystem ada di scene — WAJIB agar tombol UI bisa menerima klik mouse.
    /// Jika tidak ada, buat otomatis.
    /// </summary>
    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
            Debug.Log("[GameHUD] EventSystem dibuat otomatis — tombol pause sekarang bisa diklik.");
        }
    }

    /// <summary>Buat panel (Image + RectTransform) dengan posisi dan ukuran tertentu.</summary>
    private GameObject MakePanel(string name, Transform parent,
        Vector2 pivot, Vector2 anMin, Vector2 anMax, Vector2 size, Vector2 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.pivot            = pivot;
        rt.anchorMin        = anMin;
        rt.anchorMax        = anMax;
        rt.sizeDelta        = size;
        rt.anchoredPosition = pos;
        go.AddComponent<Image>().color = Color.clear; // Default transparan
        return go;
    }

    /// <summary>Buat TextMeshProUGUI.</summary>
    private TextMeshProUGUI MakeTMP(string name, Transform parent,
        string text, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = size;
        tmp.color              = color;
        tmp.alignment          = align;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    /// <summary>Set warna Image pada GameObject.</summary>
    private void SetImg(GameObject go, Color color)
    {
        Image img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = color;
    }

    /// <summary>Set ColorBlock pada Button.</summary>
    private void SetBtnColors(Button btn, Color normal, Color highlight)
    {
        ColorBlock cb      = btn.colors;
        cb.normalColor      = normal;
        cb.highlightedColor = highlight;
        cb.pressedColor     = new Color(normal.r * 0.72f, normal.g * 0.72f, normal.b * 0.72f, 1f);
        cb.selectedColor    = normal;
        btn.colors          = cb;
    }

    /// <summary>Set RectTransform dengan anchor dan posisi eksplisit.</summary>
    private void RectRect(RectTransform rt, Vector2 anMin, Vector2 anMax,
        Vector2 pivot, Vector2 size, Vector2 pos)
    {
        rt.anchorMin        = anMin;
        rt.anchorMax        = anMax;
        rt.pivot            = pivot;
        rt.sizeDelta        = size;
        rt.anchoredPosition = pos;
    }

    /// <summary>Stretch RectTransform mengisi parent dengan offset.</summary>
    private void Stretch(RectTransform rt,
        Vector2 offsetMin, Vector2 offsetMax, Vector2 anMin, Vector2 anMax)
    {
        rt.anchorMin = anMin;
        rt.anchorMax = anMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    // Shorthand Vector2
    private static Vector2 V2(float x, float y) => new Vector2(x, y);
}
