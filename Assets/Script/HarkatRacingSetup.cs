// ╔══════════════════════════════════════════════════════════════════════╗
// ║  HarkatRacingSetup.cs  —  UNITY EDITOR SETUP SCRIPT                 ║
// ║  Letakkan di folder: Assets/Script/                                  ║
// ║                                                                       ║
// ║  Cara Pakai:                                                          ║
// ║    1. Buka Unity Editor                                               ║
// ║    2. Buka scene game kamu (track1.unity atau track2.unity)           ║
// ║    3. Klik menu: Harkat Racing > Setup Scene Otomatis                 ║
// ║    4. Semua setup dilakukan otomatis!                                 ║
// ╚══════════════════════════════════════════════════════════════════════╝

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool untuk setup otomatis scene Harkat Racing 3D.
/// Hanya aktif di Unity Editor, tidak ikut build game.
/// </summary>
public static class HarkatRacingSetup
{
    // ────────────────────────────────────────────────────────────────────
    // Nama-nama yang dikenali sebagai coin / booster di scene
    // Tambahkan nama sesuai GameObject yang kamu buat di scene
    // ────────────────────────────────────────────────────────────────────
    private static readonly string[] CoinKeywords    = { "coin", "gold", "koin" };
    private static readonly string[] BoosterKeywords = { "boost", "booster", "nitro", "turbo", "power" };

    // ────────────────────────────────────────────────────────────────────
    [MenuItem("Harkat Racing/🔧 Setup Scene Otomatis", priority = 1)]
    public static void SetupScene()
    {
        int success = 0;
        int warn    = 0;

        Debug.Log("══════ HARKAT RACING SETUP DIMULAI ══════");

        // ── 1. Setup mobil (tag Player) ──────────────────────────────
        success += SetupCar(ref warn);

        // ── 2. Setup Coin objects ────────────────────────────────────
        success += SetupCoins(ref warn);

        // ── 3. Setup Booster objects ─────────────────────────────────
        success += SetupBoosters(ref warn);

        // ── 4. Setup GameHUD ─────────────────────────────────────────
        success += SetupGameHUD(ref warn);

        // ── Simpan scene ─────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        string summary = $"Setup selesai!\n✅ {success} item berhasil disetup\n⚠️ {warn} peringatan (baca Console untuk detail)";
        EditorUtility.DisplayDialog("Harkat Racing Setup", summary, "OK");
        Debug.Log($"══════ SETUP SELESAI: {success} berhasil, {warn} peringatan ══════");
    }

    // ════════════════════════════════════════════════════════════════════
    // SETUP FUNCTIONS
    // ════════════════════════════════════════════════════════════════════

    // ── Setup Mobil/Motor → Tag "Player" ───────────────────────────────────────
    private static int SetupCar(ref int warn)
    {
        int count = 0;

        // Cari berdasarkan komponen mobil.cs
        mobil[] cars = Object.FindObjectsByType<mobil>(FindObjectsSortMode.None);
        foreach (var car in cars)
        {
            if (car.tag != "Player")
            {
                Undo.RecordObject(car.gameObject, "Set Tag Player");
                car.gameObject.tag = "Player";
                EditorUtility.SetDirty(car.gameObject);
                Debug.Log($"✅  Mobil '{car.gameObject.name}' → Tag diset ke 'Player'");
            }
            else
            {
                Debug.Log($"ℹ️   Mobil '{car.gameObject.name}' sudah bertag 'Player'");
            }
            count++;

            // Pastikan ada Rigidbody
            if (car.GetComponent<Rigidbody>() == null)
            {
                Debug.LogWarning($"⚠️  Mobil '{car.gameObject.name}' tidak punya Rigidbody! Tambahkan secara manual.");
                warn++;
            }

            // Pastikan ada MinimapController untuk minimap Top-Down
            if (car.GetComponent<MinimapController>() == null)
            {
                Undo.RecordObject(car.gameObject, "Add MinimapController");
                Undo.AddComponent<MinimapController>(car.gameObject);
                EditorUtility.SetDirty(car.gameObject);
                Debug.Log($"✅  Mobil '{car.gameObject.name}' → MinimapController ditambahkan");
            }
        }

        // Cari berdasarkan komponen BikeController.cs
        BikeController[] bikes = Object.FindObjectsByType<BikeController>(FindObjectsSortMode.None);
        foreach (var bike in bikes)
        {
            if (bike.tag != "Player")
            {
                Undo.RecordObject(bike.gameObject, "Set Tag Player");
                bike.gameObject.tag = "Player";
                EditorUtility.SetDirty(bike.gameObject);
                Debug.Log($"✅  Motor '{bike.gameObject.name}' → Tag diset ke 'Player'");
            }
            else
            {
                Debug.Log($"ℹ️   Motor '{bike.gameObject.name}' sudah bertag 'Player'");
            }
            count++;

            // Pastikan ada Rigidbody
            if (bike.GetComponent<Rigidbody>() == null)
            {
                Debug.LogWarning($"⚠️  Motor '{bike.gameObject.name}' tidak punya Rigidbody! Tambahkan secara manual.");
                warn++;
            }

            // Pastikan ada MinimapController untuk minimap Top-Down
            if (bike.GetComponent<MinimapController>() == null)
            {
                Undo.RecordObject(bike.gameObject, "Add MinimapController");
                Undo.AddComponent<MinimapController>(bike.gameObject);
                EditorUtility.SetDirty(bike.gameObject);
                Debug.Log($"✅  Motor '{bike.gameObject.name}' → MinimapController ditambahkan");
            }
        }

        if (count == 0)
        {
            Debug.LogWarning("⚠️  Tidak menemukan GameObject dengan script 'mobil.cs' atau 'BikeController.cs'. " +
                             "Tambahkan salah satu script tersebut ke kendaraan pemain dulu.");
            warn++;
            return 0;
        }

        return count;
    }

    // ── Setup Coins → CoinPickup + Collider (IsTrigger) ─────────────────
    private static int SetupCoins(ref int warn)
    {
        GameObject[] allObjects = GetAllSceneObjects();
        int count = 0;

        foreach (var go in allObjects)
        {
            if (!IsCoin(go.name)) continue;

            // Tambah CoinPickup jika belum ada
            CoinPickup pickup = go.GetComponent<CoinPickup>();
            if (pickup == null)
            {
                Undo.RecordObject(go, "Add CoinPickup");
                pickup = Undo.AddComponent<CoinPickup>(go);
                Debug.Log($"✅  '{go.name}' → CoinPickup ditambahkan");
            }
            else
            {
                Debug.Log($"ℹ️   '{go.name}' sudah punya CoinPickup");
            }

            // Pastikan ada Collider dengan IsTrigger = true
            bool hasCollider = EnsureTriggerCollider(go, "Coin");
            if (!hasCollider) warn++;

            count++;
        }

        if (count == 0)
        {
            Debug.LogWarning("⚠️  Tidak menemukan GameObject Coin.\n" +
                             "Pastikan nama GameObject coin mengandung kata: " +
                             string.Join(", ", CoinKeywords) + "\n" +
                             "(tidak case-sensitive). Atau pasang CoinPickup.cs secara manual.");
            warn++;
        }
        else
        {
            Debug.Log($"✅  {count} Coin berhasil disetup");
        }

        return count;
    }

    // ── Setup Boosters → BoosterPickup + Collider (IsTrigger) ───────────
    private static int SetupBoosters(ref int warn)
    {
        GameObject[] allObjects = GetAllSceneObjects();
        int count = 0;

        foreach (var go in allObjects)
        {
            if (!IsBooster(go.name)) continue;

            // Tambah BoosterPickup jika belum ada
            BoosterPickup pickup = go.GetComponent<BoosterPickup>();
            if (pickup == null)
            {
                Undo.RecordObject(go, "Add BoosterPickup");
                pickup = Undo.AddComponent<BoosterPickup>(go);
                Debug.Log($"✅  '{go.name}' → BoosterPickup ditambahkan");
            }
            else
            {
                Debug.Log($"ℹ️   '{go.name}' sudah punya BoosterPickup");
            }

            // Pastikan ada Collider dengan IsTrigger = true
            bool hasCollider = EnsureTriggerCollider(go, "Booster");
            if (!hasCollider) warn++;

            count++;
        }

        if (count == 0)
        {
            Debug.LogWarning("⚠️  Tidak menemukan GameObject Booster.\n" +
                             "Pastikan nama GameObject booster mengandung kata: " +
                             string.Join(", ", BoosterKeywords) + "\n" +
                             "(tidak case-sensitive). Atau pasang BoosterPickup.cs secara manual.");
            warn++;
        }
        else
        {
            Debug.Log($"✅  {count} Booster berhasil disetup");
        }

        return count;
    }

    // ── Setup GameHUD GameObject ─────────────────────────────────────────
    private static int SetupGameHUD(ref int warn)
    {
        // Cek apakah sudah ada GameHUD di scene
        GameHUD existing = Object.FindFirstObjectByType<GameHUD>();
        if (existing != null)
        {
            // Update mainMenuSceneName jika masih default
            existing.mainMenuSceneName = "MainMenu";
            existing.totalCoins        = CountCoins();
            EditorUtility.SetDirty(existing);
            Debug.Log($"ℹ️   GameHUD sudah ada di '{existing.gameObject.name}'. " +
                      $"mainMenuSceneName='MainMenu', totalCoins={existing.totalCoins}");
            return 1;
        }

        // Buat GameObject baru bernama "GameHUD"
        GameObject hudGO = new GameObject("GameHUD");
        Undo.RegisterCreatedObjectUndo(hudGO, "Create GameHUD");

        GameHUD hud = hudGO.AddComponent<GameHUD>();
        hud.mainMenuSceneName = "MainMenu";
        hud.totalCoins        = CountCoins();

        EditorUtility.SetDirty(hudGO);

        Debug.Log($"✅  GameHUD dibuat. mainMenuSceneName='MainMenu', totalCoins={hud.totalCoins}");
        return 1;
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════

    // Pastikan ada SphereCollider (atau collider apa saja) dengan Is Trigger = true
    private static bool EnsureTriggerCollider(GameObject go, string type)
    {
        Collider[] colliders = go.GetComponents<Collider>();

        if (colliders.Length == 0)
        {
            // Tidak ada collider sama sekali → tambah SphereCollider sebagai trigger
            Undo.RecordObject(go, $"Add Trigger Collider to {type}");
            SphereCollider sc = Undo.AddComponent<SphereCollider>(go);
            sc.isTrigger = true;
            sc.radius    = 0.8f;
            Debug.Log($"  ↳ '{go.name}' tidak punya Collider → SphereCollider trigger ditambahkan (radius 0.8)");
            return true;
        }

        // Ada collider, pastikan IsTrigger = true
        bool allTrigger = true;
        foreach (var col in colliders)
        {
            if (!col.isTrigger)
            {
                Undo.RecordObject(col, "Set IsTrigger");
                col.isTrigger = true;
                EditorUtility.SetDirty(col);
                Debug.Log($"  ↳ '{go.name}' Collider ({col.GetType().Name}) → Is Trigger diaktifkan");
            }
        }

        return allTrigger;
    }

    // Hitung jumlah coin di scene (untuk field totalCoins di GameHUD)
    private static int CountCoins()
    {
        int count = 0;
        foreach (var go in GetAllSceneObjects())
            if (IsCoin(go.name)) count++;
        return count;
    }

    // Cek apakah nama GameObject mengandung keyword coin
    private static bool IsCoin(string name)
    {
        string lower = name.ToLower();
        foreach (var kw in CoinKeywords)
            if (lower.Contains(kw)) return true;
        return false;
    }

    // Cek apakah nama GameObject mengandung keyword booster
    private static bool IsBooster(string name)
    {
        string lower = name.ToLower();
        foreach (var kw in BoosterKeywords)
            if (lower.Contains(kw)) return true;
        return false;
    }

    // Ambil semua GameObject di scene (termasuk yang di dalam hierarchy)
    private static GameObject[] GetAllSceneObjects()
    {
        return Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    // ────────────────────────────────────────────────────────────────────
    // Menu tambahan: Laporan nama GameObject di scene (untuk debug)
    // ────────────────────────────────────────────────────────────────────
    [MenuItem("Harkat Racing/📋 Lihat Nama Semua GameObject", priority = 2)]
    public static void PrintAllObjects()
    {
        Debug.Log("══════ DAFTAR SEMUA GAMEOBJECT DI SCENE ══════");
        foreach (var go in GetAllSceneObjects())
        {
            string components = "";
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null) components += c.GetType().Name + ", ";
            }
            Debug.Log($"  {go.name}  [komponen: {components}]");
        }
        Debug.Log("══════════════════════════════════════════════");
    }

    // ────────────────────────────────────────────────────────────────────
    // Menu: Tambah keyword coin/booster manual jika nama tidak terdeteksi
    // ────────────────────────────────────────────────────────────────────
    [MenuItem("Harkat Racing/🪙 Tandai Selection sebagai COIN", priority = 10)]
    public static void MarkAsCoin()
    {
        int count = 0;
        foreach (var go in Selection.gameObjects)
        {
            CoinPickup pickup = go.GetComponent<CoinPickup>();
            if (pickup == null)
            {
                Undo.RecordObject(go, "Add CoinPickup");
                Undo.AddComponent<CoinPickup>(go);
            }
            EnsureTriggerCollider(go, "Coin");
            count++;
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"✅  {count} GameObject ditandai sebagai Coin");
    }

    [MenuItem("Harkat Racing/⚡ Tandai Selection sebagai BOOSTER", priority = 11)]
    public static void MarkAsBooster()
    {
        int count = 0;
        foreach (var go in Selection.gameObjects)
        {
            BoosterPickup pickup = go.GetComponent<BoosterPickup>();
            if (pickup == null)
            {
                Undo.RecordObject(go, "Add BoosterPickup");
                Undo.AddComponent<BoosterPickup>(go);
            }
            EnsureTriggerCollider(go, "Booster");
            count++;
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"✅  {count} GameObject ditandai sebagai Booster");
    }

    [MenuItem("Harkat Racing/🪙 Tandai Selection sebagai COIN", validate = true)]
    [MenuItem("Harkat Racing/⚡ Tandai Selection sebagai BOOSTER", validate = true)]
    private static bool ValidateSelection() => Selection.gameObjects.Length > 0;
}
#endif
