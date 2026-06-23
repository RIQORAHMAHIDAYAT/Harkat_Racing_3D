using UnityEngine;

/// <summary>
/// VehicleRespawn — Komponen universal untuk respawn & anti-stuck.
/// Bisa dipasang di kendaraan apa pun (motor/mobil) yang punya Rigidbody.
///
/// Fitur:
///   • Track posisi aman terakhir (saat melaju > threshold speed)
///   • Respawn manual tekan tombol R
///   • Auto-respawn saat jatuh di bawah Y threshold
///   • Auto-respawn saat stuck (kecepatan < threshold > waktu tertentu)
/// </summary>
public class VehicleRespawn : MonoBehaviour
{
    [Header("Respawn Settings")]
    [SerializeField] private KeyCode respawnKey = KeyCode.R;
    [SerializeField] private float safeSpeedThreshold = 5f;
    [SerializeField] private float stuckSpeedThreshold = 0.5f;
    [SerializeField] private float autoRespawnTime = 6f;
    [SerializeField] private float fallThresholdY = -5f;
    [SerializeField] private float respawnCooldown = 3f;
    [SerializeField] private float respawnHeightOffset = 2f;

    private Rigidbody rb;
    private Vector3 lastSafePosition;
    private Quaternion lastSafeRotation;
    private float stuckTimer = 0f;
    private float cooldownTimer = 0f;
    private bool hasSafePosition = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        lastSafePosition = transform.position;
        lastSafeRotation = transform.rotation;
        hasSafePosition = true;
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(respawnKey) && cooldownTimer <= 0f)
            DoRespawn();

        if (transform.position.y < fallThresholdY)
            DoRespawn();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;
        float speed = rb.linearVelocity.magnitude;

        if (speed > safeSpeedThreshold)
        {
            lastSafePosition = transform.position;
            lastSafeRotation = transform.rotation;
            hasSafePosition = true;
            stuckTimer = 0f;
        }
        else if (speed < stuckSpeedThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > autoRespawnTime)
                DoRespawn();
        }
    }

    private void DoRespawn()
    {
        if (cooldownTimer > 0f) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (!hasSafePosition)
        {
            lastSafePosition = transform.position + Vector3.up * 2f;
            lastSafeRotation = transform.rotation;
        }

        transform.position = lastSafePosition + Vector3.up * respawnHeightOffset;
        transform.rotation = lastSafeRotation;

        stuckTimer = 0f;
        cooldownTimer = respawnCooldown;
    }

    private void OnDrawGizmosSelected()
    {
        if (hasSafePosition)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lastSafePosition, 0.5f);
            Gizmos.DrawLine(transform.position, lastSafePosition);
        }
    }
}
