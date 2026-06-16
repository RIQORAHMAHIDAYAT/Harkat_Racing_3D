using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Mengontrol pengaturan Music, Sound, dan Control Mode di panel Settings.
/// Pasang script ini di GameObject PanelSetting.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    [Header("Music Toggle")]
    public Image musicToggleImage;
    public TextMeshProUGUI musicToggleText;
    private bool musicEnabled = true;

    [Header("Sound Toggle")]
    public Image soundToggleImage;
    public TextMeshProUGUI soundToggleText;
    private bool soundEnabled = true;

    [Header("Control Mode")]
    public TextMeshProUGUI controlModeText;
    private int controlModeIndex = 0;
    private readonly string[] controlModes = { "Keyboard", "Gamepad", "Mobile" };

    // Warna tombol
    private readonly Color colorOn  = new Color(0.20f, 0.78f, 0.35f, 1f);  // hijau
    private readonly Color colorOff = new Color(0.80f, 0.22f, 0.22f, 1f);  // merah
    private readonly Color colorCtrl = new Color(0.22f, 0.50f, 0.87f, 1f); // biru

    void Start()
    {
        UpdateMusicUI();
        UpdateSoundUI();
        UpdateControlUI();
    }

    /// <summary>Toggle status Music On/Off.</summary>
    public void ToggleMusic()
    {
        musicEnabled = !musicEnabled;
        UpdateMusicUI();
        // TODO: hubungkan ke AudioManager jika sudah ada
        // AudioManager.Instance?.SetMusicEnabled(musicEnabled);
    }

    /// <summary>Toggle status Sound On/Off.</summary>
    public void ToggleSound()
    {
        soundEnabled = !soundEnabled;
        UpdateSoundUI();
        // TODO: hubungkan ke AudioManager jika sudah ada
        // AudioManager.Instance?.SetSoundEnabled(soundEnabled);
    }

    /// <summary>Siklus mode kontrol: Keyboard → Gamepad → Mobile → Keyboard.</summary>
    public void NextControlMode()
    {
        controlModeIndex = (controlModeIndex + 1) % controlModes.Length;
        UpdateControlUI();
        // TODO: simpan dan terapkan mode kontrol yang dipilih
    }

    private void UpdateMusicUI()
    {
        if (musicToggleImage != null) musicToggleImage.color = musicEnabled ? colorOn : colorOff;
        if (musicToggleText  != null) musicToggleText.text  = musicEnabled ? "ON" : "OFF";
    }

    private void UpdateSoundUI()
    {
        if (soundToggleImage != null) soundToggleImage.color = soundEnabled ? colorOn : colorOff;
        if (soundToggleText  != null) soundToggleText.text  = soundEnabled ? "ON" : "OFF";
    }

    private void UpdateControlUI()
    {
        if (controlModeText != null) controlModeText.text = controlModes[controlModeIndex];
    }
}
