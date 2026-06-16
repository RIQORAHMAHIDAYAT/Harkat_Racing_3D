using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Mengontrol tampilan popup panel Settings dan About pada Main Menu.
/// Pasang script ini di GameObject Canvas.
/// </summary>
public class MenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject panelSettings;
    public GameObject panelAbout;

    void Start()
    {
        // Pastikan semua panel tersembunyi saat game dimulai
        CloseAllPanels();
        ConfigureAboutPanel();
    }

    /// <summary>Membuka panel Settings dan menutup panel lain.</summary>
    public void OpenSettings()
    {
        if (panelSettings != null) panelSettings.SetActive(true);
        if (panelAbout != null) panelAbout.SetActive(false);
    }

    /// <summary>Membuka panel About dan menutup panel lain.</summary>
    public void OpenAbout()
    {
        if (panelAbout != null) panelAbout.SetActive(true);
        if (panelSettings != null) panelSettings.SetActive(false);
    }

    /// <summary>Menutup semua panel yang sedang terbuka.</summary>
    public void CloseAllPanels()
    {
        if (panelSettings != null) panelSettings.SetActive(false);
        if (panelAbout != null) panelAbout.SetActive(false);
    }

    /// <summary>Mengonfigurasi layout dan teks Panel About secara dinamis di runtime.</summary>
    private void ConfigureAboutPanel()
    {
        if (panelAbout == null) return;

        // Pastikan ukuran panel About cukup besar
        RectTransform panelRect = panelAbout.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.sizeDelta = new Vector2(660f, 600f);
        }

        // Cari dan konfigurasi TextMeshProUGUI untuk deskripsi dan judul
        TextMeshProUGUI[] texts = panelAbout.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in texts)
        {
            RectTransform txtRect = txt.GetComponent<RectTransform>();
            if (txtRect == null) continue;

            // Konfigurasi khusus untuk deskripsi (berada di tengah/bawah panel, y < 100)
            if (txt.gameObject.name == "AboutDescText" || (txt.gameObject.name == "Text (TMP)" && txtRect.anchoredPosition.y < 100f))
            {
                txt.fontSize = 16f;
                txt.enableAutoSizing = true;
                txt.fontSizeMin = 12f;
                txt.fontSizeMax = 20f;
                txt.alignment = TextAlignmentOptions.TopLeft;

                txtRect.anchoredPosition = new Vector2(0f, 20f);
                txtRect.sizeDelta = new Vector2(580f, 400f);

                txt.text = "Harkat Racing 2026 v1.1.1\n\n" +
                           "Harkat Racing 2026 is a racing game that delivers an exciting driving experience " +
                           "through various tracks and challenges. Take control of your favorite vehicle, " +
                           "achieve the best lap times, and prove your racing skills in every competition.\n\n" +
                           "Version: 1.1.1\n" +
                           "Release Year: 2026\n" +
                           "Developer: Rq (23090165)";
            }
            // Konfigurasi khusus untuk judul (berada di atas panel, y >= 100)
            else if (txt.gameObject.name == "AboutTitleText" || (txt.gameObject.name == "Text (TMP)" && txtRect.anchoredPosition.y >= 100f))
            {
                txt.text = "ABOUT";
                txt.fontSize = 40f;
                txt.enableAutoSizing = false;
                txt.alignment = TextAlignmentOptions.Center;
            }
            // Pastikan teks tombol Close kembali normal dan tidak terpengaruh
            else if (txt.gameObject.name == "CloseAboutBtnText")
            {
                txt.text = "Close";
                txt.fontSize = 18f;
                txt.enableAutoSizing = false;
                txt.alignment = TextAlignmentOptions.Center;

                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.anchoredPosition = Vector2.zero;
                txtRect.sizeDelta = Vector2.zero;
            }
        }

        // Cari dan atur posisi tombol Close agar berada di paling bawah
        Button closeBtn = panelAbout.GetComponentInChildren<Button>(true);
        if (closeBtn != null)
        {
            RectTransform btnRect = closeBtn.GetComponent<RectTransform>();
            if (btnRect != null)
            {
                btnRect.anchoredPosition = new Vector2(0f, -240f);
            }
        }
    }
}
