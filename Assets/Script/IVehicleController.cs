using UnityEngine;

/// <summary>
/// IVehicleController — Interface untuk semua kendaraan (mobil, motor, dll.)
/// agar dapat dikontrol oleh sistem eksternal seperti GameHUD dan BoosterPickup.
/// </summary>
public interface IVehicleController
{
    /// <summary>Mengunci atau melepas input pemain (untuk countdown/pause).</summary>
    void SetInputLocked(bool locked);

    /// <summary>Mengaktifkan efek speed boost selama durasi tertentu.</summary>
    void ActivateBoost(float duration);
}
