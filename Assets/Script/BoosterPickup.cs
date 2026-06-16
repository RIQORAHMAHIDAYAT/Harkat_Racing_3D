using UnityEngine;

/// <summary>
/// BoosterPickup — Pasang script ini di setiap GameObject Booster di scene.
///
/// Setup yang diperlukan di Unity Editor:
///   1. Tambahkan Collider ke GameObject Booster
///   2. Centang "Is Trigger" di Collider tersebut
///   3. Pastikan GameObject mobil pemain memiliki Tag "Player"
/// </summary>
public class BoosterPickup : MonoBehaviour
{
    // =========================================================
    // SETTINGS
    // =========================================================
    [Header("Booster Settings")]
    [Tooltip("Durasi boost kecepatan dalam detik")]
    [SerializeField] private float boostDuration = 3f;

    [Header("Booster Animation")]
    [Tooltip("Tinggi hover naik-turun")]
    [SerializeField] private float hoverAmplitude = 0.18f;

    [Tooltip("Kecepatan hover")]
    [SerializeField] private float hoverSpeed = 1.8f;

    [Tooltip("Kecepatan rotasi booster")]
    [SerializeField] private float rotationSpeed = 80f;

    // =========================================================
    // PRIVATE
    // =========================================================
    private bool    _collected = false;
    private Vector3 _startPos;

    // =========================================================
    // LIFECYCLE
    // =========================================================
    private void Start()
    {
        _startPos = transform.position;
    }

    private void Update()
    {
        // Animasi rotasi booster
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // Animasi hover naik-turun (lebih dramatis dari coin)
        float yOffset = Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude;
        transform.position = new Vector3(_startPos.x, _startPos.y + yOffset, _startPos.z);
    }

    // =========================================================
    // TRIGGER
    // =========================================================
    private void OnTriggerEnter(Collider other)
    {
        if (_collected) return;                     // Cegah double-collect
        if (!other.CompareTag("Player")) return;    // Hanya mobil pemain

        _collected = true;

        // Cari script mobil — cek di collider atau parent-nya
        mobil car = other.GetComponent<mobil>();
        if (car == null) car = other.GetComponentInParent<mobil>();
        if (car == null) car = other.GetComponentInChildren<mobil>();

        // Aktifkan boost kecepatan
        if (car != null)
            car.ActivateBoost(boostDuration);

        // Tampilkan indikator boost di HUD
        if (GameHUD.Instance != null)
            GameHUD.Instance.ShowBoostActivated(boostDuration);

        // Sembunyikan booster (seolah terambil)
        gameObject.SetActive(false);
    }
}
