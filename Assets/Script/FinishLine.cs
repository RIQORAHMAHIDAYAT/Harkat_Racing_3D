using UnityEngine;

/// <summary>
/// FinishLine — Dipasang di GameObject garis finis di scene.
///
/// Cara setup di Unity Editor:
///   1. Buat GameObject kosong di posisi garis finis (misal: "FinishLine")
///   2. Tambahkan Collider (BoxCollider direkomendasikan) — luaskan agar menutupi lebar sirkuit
///   3. Centang "Is Trigger" pada Collider
///   4. Drag script FinishLine.cs ke GameObject tersebut
///   5. Pastikan mobil pemain memiliki Tag "Player"
///
/// Catatan:
///   - Jika sirkuit hanya 1 lap, mobil langsung finish saat melewati garis
///   - Jika multi-lap, atur field "Laps Required" di Inspector
/// </summary>
public class FinishLine : MonoBehaviour
{
    [Header("Finish Settings")]
    [Tooltip("Jumlah lap yang harus diselesaikan sebelum finish")]
    [SerializeField] private int lapsRequired = 1;

    [Tooltip("Jarak minimum (detik) antara crossing pertama dan kedua untuk menghindari false trigger saat start")]
    [SerializeField] private float minTimeBetweenCross = 5f;

    // ─────────────────────────────────────────────────────────────
    private int   _lapsCrossed     = 0;
    private float _lastCrossTime   = 0f;
    private bool  _raceFinished    = false;

    private void Start()
    {
        // Set waktu awal agar jika mobil menyentuh garis finis di awal balapan (start),
        // trigger tersebut diabaikan (karena masih dalam minTimeBetweenCross).
        _lastCrossTime = Time.time;
    }

    // ─────────────────────────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (_raceFinished) return;
        if (!other.CompareTag("Player")) return;

        float now = Time.time;

        // Abaikan crossing pertama jika terlalu cepat dari awal (saat start dekat garis)
        if (now - _lastCrossTime < minTimeBetweenCross)
        {
            Debug.Log($"[FinishLine] Crossing diabaikan (terlalu cepat: {now - _lastCrossTime:F1}s)");
            return;
        }

        _lastCrossTime = now;
        _lapsCrossed++;

        Debug.Log($"[FinishLine] Lap {_lapsCrossed}/{lapsRequired} selesai!");

        if (_lapsCrossed >= lapsRequired)
        {
            _raceFinished = true;
            Debug.Log("[FinishLine] FINISH! Semua lap selesai.");

            // Beritahu GameHUD
            if (GameHUD.Instance != null)
                GameHUD.Instance.TriggerFinish();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Visualisasi di Scene view (gizmo garis finis)
    // ─────────────────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        // Gambar kotak hijau menandakan area trigger finish
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
            Gizmos.DrawCube(box.center, box.size);
        else
            Gizmos.DrawCube(Vector3.zero, Vector3.one);

        // Border solid
        Gizmos.color = new Color(0f, 1f, 0.3f, 1f);
        if (col is BoxCollider box2)
            Gizmos.DrawWireCube(box2.center, box2.size);
        else
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }

    private void OnDrawGizmosSelected()
    {
        // Label di scene view
        Gizmos.color = Color.white;
        // UnityEditor namespace tidak tersedia di build — label hanya via editor script
    }
}
