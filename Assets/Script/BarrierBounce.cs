using UnityEngine;

/// <summary>
/// BarrierBounce — Tambahkan ke GameObject barrier/tembok yang punya Collider.
///
/// Pada saat Start, script ini akan membuat PhysicsMaterial dengan bounce
/// dan gesekan rendah, lalu menetapkannya ke Collider.
///
/// Efek: kendaraan yang menabrak barrier akan terpantul ringan,
///       mencegah motor menyangkut di pinggir tembok.
/// </summary>
public class BarrierBounce : MonoBehaviour
{
    [Header("Barrier Physics")]
    [SerializeField] private float bounciness = 0.45f;
    [SerializeField] private float friction = 0.1f;

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        PhysicsMaterial mat = new PhysicsMaterial("Bounce_Barrier_" + gameObject.name);
        mat.dynamicFriction = friction;
        mat.staticFriction = friction;
        mat.bounciness = bounciness;
        mat.frictionCombine = PhysicsMaterialCombine.Minimum;
        mat.bounceCombine = PhysicsMaterialCombine.Maximum;

        col.material = mat;
    }
}
