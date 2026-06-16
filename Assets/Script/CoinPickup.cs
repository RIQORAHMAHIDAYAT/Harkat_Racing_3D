using UnityEngine;

/// <summary>
/// CoinPickup — Pasang script ini di setiap GameObject Coin di scene.
///
/// Setup yang diperlukan di Unity Editor:
///   1. Tambahkan Collider (misal: CapsuleCollider atau SphereCollider) ke GameObject Coin
///   2. Centang "Is Trigger" di Collider tersebut
///   3. Pastikan GameObject mobil pemain memiliki Tag "Player"
/// </summary>
public class CoinPickup : MonoBehaviour
{
    // =========================================================
    // SETTINGS
    // =========================================================
    [Header("Coin Animation")]
    [Tooltip("Kecepatan rotasi coin (derajat per detik)")]
    [SerializeField] private float rotationSpeed = 130f;

    [Tooltip("Tinggi hover naik-turun di atas posisi awal")]
    [SerializeField] private float hoverAmplitude = 0.12f;

    [Tooltip("Kecepatan hover naik-turun")]
    [SerializeField] private float hoverSpeed = 2.2f;

    // =========================================================
    // PRIVATE
    // =========================================================
    private bool    _collected   = false;
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
        // Animasi rotasi agar coin terlihat berputar
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // Animasi hover naik-turun
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

        // Lapor ke GameHUD untuk update counter
        if (GameHUD.Instance != null)
            GameHUD.Instance.AddCoin();

        // Sembunyikan coin (seolah terambil)
        gameObject.SetActive(false);
    }
}
