using UnityEngine;
using System.Collections;

public class mobil : MonoBehaviour
{
    private float horizontalInput;
    private float verticalInput;
    private float currentSteerAngle;
    private float currentBrakeForce;
    private bool isBraking;

    // =========================
    // BOOST & INPUT LOCK
    // =========================
    private bool      _inputLocked       = false;
    private float     _defaultMotorForce = 0f;
    private Coroutine _boostCoroutine    = null;

    // =========================
    // SETTINGS
    // =========================
    [Header("Car Settings")]
    [SerializeField] private float motorForce = 1500f;
    [SerializeField] private float brakeForce = 3000f;
    [SerializeField] private float maxSteerAngle = 38f; // Sudut belok maks pada kecepatan rendah (lincah)
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.6f, 0f); // Menurunkan center of mass agar mobil stabil

    [Header("Advanced Steering Settings")]
    [SerializeField] private float steerResponsiveSpeed = 220f; // Kecepatan setir berputar (derajat per detik) saat belok
    [SerializeField] private float steerCenterSpeed = 450f; // Kecepatan setir kembali ke tengah (derajat per detik) saat dilepas
    [SerializeField] private float minSteerAngleAtHighSpeed = 22f; // Sudut belok minimal pada kecepatan tinggi (agar tetap bisa belok di tikungan tajam)
    [SerializeField] private float speedForMinSteer = 90f; // Batas kecepatan (km/h) di mana sudut belok mencapai nilai minimal

    [Header("Drift / Friction Settings")]
    [SerializeField] private float frontWheelStiffness = 1.4f; // Grip ban depan (tinggi = menggigit, stabil)
    [SerializeField] private float normalSidewaysStiffness = 1.4f; // Grip ban belakang saat jalan lurus/biasa (tinggi = tidak mudah melintir sendiri)
    [SerializeField] private float driftSidewaysStiffness = 0.35f; // Grip ban belakang diturunkan saat drift agar slide terkendali
    [SerializeField] private float stiffnessTransitionSpeed = 8.0f; // Kecepatan pemulihan grip setelah melepas rem tangan

    [Header("Physics Downforce")]
    [SerializeField] private float downforce = 80f; // Gaya tekan ke bawah untuk menambah stabilitas grip ban di kecepatan tinggi

    // =========================
    // WHEEL COLLIDERS
    // =========================
    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeftWheelCollider;
    [SerializeField] private WheelCollider frontRightWheelCollider;
    [SerializeField] private WheelCollider rearLeftWheelCollider;
    [SerializeField] private WheelCollider rearRightWheelCollider;

    // =========================
    // WHEEL VISUALS
    // =========================
    [Header("Wheel Transforms")]
    [SerializeField] private Transform frontLeftWheelTransform;
    [SerializeField] private Transform frontRightWheelTransform;
    [SerializeField] private Transform rearLeftWheelTransform;
    [SerializeField] private Transform rearRightWheelTransform;

    private Rigidbody carRigidbody;

    private void Start()
    {
        carRigidbody = GetComponent<Rigidbody>();
        if (carRigidbody != null)
        {
            // Atur Center of Mass lebih rendah untuk stabilitas tinggi saat menikung
            carRigidbody.centerOfMass = centerOfMassOffset;
        }

        // Inisialisasi cengkraman (stiffness) ban depan dan belakang secara konsisten sejak awal game dimulai
        InitializeFrictionValues();

        // Simpan nilai motorForce awal sebagai baseline sebelum boost diterapkan
        _defaultMotorForce = motorForce;
    }

    private void Update()
    {
        // Pindahkan GetInput dan UpdateWheels ke Update untuk visualisasi yang super mulus (tanpa getar)
        GetInput();
        UpdateWheels();
    }

    private void FixedUpdate()
    {
        // Operasi fisika tetap berada di FixedUpdate
        HandleMotor();
        HandleSteering();
        HandleFriction();
        ApplyDownforce();
    }

    // =========================
    // INITIALIZE FRICTION
    // =========================
    private void InitializeFrictionValues()
    {
        // Ban Depan (Statis)
        WheelFrictionCurve frontLeftFriction = frontLeftWheelCollider.sidewaysFriction;
        WheelFrictionCurve frontRightFriction = frontRightWheelCollider.sidewaysFriction;
        frontLeftFriction.stiffness = frontWheelStiffness;
        frontRightFriction.stiffness = frontWheelStiffness;
        frontLeftWheelCollider.sidewaysFriction = frontLeftFriction;
        frontRightWheelCollider.sidewaysFriction = frontRightFriction;

        // Ban Belakang (Inisialisasi awal ke normal)
        WheelFrictionCurve rearLeftFriction = rearLeftWheelCollider.sidewaysFriction;
        WheelFrictionCurve rearRightFriction = rearRightWheelCollider.sidewaysFriction;
        rearLeftFriction.stiffness = normalSidewaysStiffness;
        rearRightFriction.stiffness = normalSidewaysStiffness;
        rearLeftWheelCollider.sidewaysFriction = rearLeftFriction;
        rearRightWheelCollider.sidewaysFriction = rearRightFriction;
    }

    // =========================
    // INPUT
    // =========================
    private void GetInput()
    {
        // Jika input dikunci (countdown / pause), abaikan semua input dari pemain
        if (_inputLocked)
        {
            horizontalInput = 0f;
            verticalInput   = 0f;
            isBraking       = false;
            return;
        }

        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput   = Input.GetAxis("Vertical");
        isBraking       = Input.GetKey(KeyCode.Space);
    }

    // =========================
    // MOTOR
    // =========================
    private void HandleMotor()
    {
        float speed = carRigidbody != null ? carRigidbody.linearVelocity.magnitude : 0f;

        // Cegah ban berputar sendiri (gelinding hantu) saat mobil diam dan tidak ditekan W/S
        if (Mathf.Abs(verticalInput) < 0.05f)
        {
            rearLeftWheelCollider.motorTorque = 0f;
            rearRightWheelCollider.motorTorque = 0f;

            if (speed < 0.15f)
            {
                // Berikan sedikit rem pengunci agar roda benar-benar berhenti berputar secara visual
                frontLeftWheelCollider.brakeTorque = 15f;
                frontRightWheelCollider.brakeTorque = 15f;
                rearLeftWheelCollider.brakeTorque = 15f;
                rearRightWheelCollider.brakeTorque = 15f;
                return;
            }
        }
        else
        {
            // RWD (penggerak belakang)
            rearLeftWheelCollider.motorTorque = verticalInput * motorForce;
            rearRightWheelCollider.motorTorque = verticalInput * motorForce;
        }

        // Brake
        currentBrakeForce = isBraking ? brakeForce : 0f;

        ApplyBraking();
    }

    // =========================
    // BRAKING
    // =========================
    private void ApplyBraking()
    {
        // Distribusi rem: roda depan rem ringan, roda belakang rem penuh (rem tangan)
        frontLeftWheelCollider.brakeTorque = isBraking ? brakeForce * 0.3f : 0f;
        frontRightWheelCollider.brakeTorque = isBraking ? brakeForce * 0.3f : 0f;

        rearLeftWheelCollider.brakeTorque = currentBrakeForce;
        rearRightWheelCollider.brakeTorque = currentBrakeForce;
    }

    // =========================
    // STEERING
    // =========================
    private void HandleSteering()
    {
        float speed = carRigidbody != null ? carRigidbody.linearVelocity.magnitude * 3.6f : 0f; // km/h

        // Speed-Sensitive Steering: kurangi sudut belok maksimal di kecepatan tinggi secara rasional
        float speedFactor = Mathf.Clamp01(speed / speedForMinSteer);
        float dynamicMaxSteer = Mathf.Lerp(maxSteerAngle, minSteerAngleAtHighSpeed, speedFactor);

        float targetSteerAngle = dynamicMaxSteer * horizontalInput;

        // Transisi belok menggunakan nilai derajat per detik yang eksplisit
        // Jika melepas tombol (horizontalInput == 0), setir kembali lurus dengan sangat cepat (steerCenterSpeed)
        // Jika sedang membelok atau berpindah arah, gunakan kecepatan standard (steerResponsiveSpeed)
        float currentSteerSpeed = (horizontalInput == 0f) ? steerCenterSpeed : steerResponsiveSpeed;

        currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteerAngle, Time.fixedDeltaTime * currentSteerSpeed);

        frontLeftWheelCollider.steerAngle = currentSteerAngle;
        frontRightWheelCollider.steerAngle = currentSteerAngle;
    }

    // =========================
    // FRICTION (DRIFT & GRIP MECHANIC)
    // =========================
    private void HandleFriction()
    {
        WheelFrictionCurve rearLeftFriction = rearLeftWheelCollider.sidewaysFriction;
        WheelFrictionCurve rearRightFriction = rearRightWheelCollider.sidewaysFriction;

        if (isBraking)
        {
            // Ketika menekan rem tangan (Space), kurangi grip samping roda belakang untuk memicu drift
            rearLeftFriction.stiffness = driftSidewaysStiffness;
            rearRightFriction.stiffness = driftSidewaysStiffness;
        }
        else
        {
            // Kembalikan ke grip belakang normal secara halus
            rearLeftFriction.stiffness = Mathf.MoveTowards(rearLeftFriction.stiffness, normalSidewaysStiffness, Time.fixedDeltaTime * stiffnessTransitionSpeed);
            rearRightFriction.stiffness = Mathf.MoveTowards(rearRightFriction.stiffness, normalSidewaysStiffness, Time.fixedDeltaTime * stiffnessTransitionSpeed);
        }

        rearLeftWheelCollider.sidewaysFriction = rearLeftFriction;
        rearRightWheelCollider.sidewaysFriction = rearRightFriction;
    }

    // =========================
    // DOWNFORCE PHYSICS
    // =========================
    private void ApplyDownforce()
    {
        if (carRigidbody != null)
        {
            // Memberikan gaya dorong vertikal ke bawah (searah lokal bawah mobil) agar roda menempel kuat di tanah
            // Semakin cepat laju mobil, semakin kuat gaya tekannya (meniru sistem spoiler aerodinamis)
            carRigidbody.AddForce(-transform.up * downforce * carRigidbody.linearVelocity.magnitude);
        }
    }

    // =========================
    // UPDATE WHEELS
    // =========================
    private void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftWheelCollider, frontLeftWheelTransform);
        UpdateSingleWheel(frontRightWheelCollider, frontRightWheelTransform);

        UpdateSingleWheel(rearLeftWheelCollider, rearLeftWheelTransform);
        UpdateSingleWheel(rearRightWheelCollider, rearRightWheelTransform);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
    {
        Vector3 pos;
        Quaternion rot;

        wheelCollider.GetWorldPose(out pos, out rot);

        wheelTransform.position = pos;

        string name = wheelTransform.name;

        if (name.Contains("FR") || name.Contains("RR"))
        {
            // Roda kanan: mirror Y + offset Z -85
            wheelTransform.rotation = rot * Quaternion.Euler(0f, 180f, -85f);
        }
        else
        {
            // Roda kiri: hanya offset Z +85
            wheelTransform.rotation = rot * Quaternion.Euler(0f, 0f, 85f);
        }
    }

    // =========================
    // PUBLIC: SPEED BOOST
    // =========================
    /// <summary>
    /// Aktifkan speed boost selama <paramref name="duration"/> detik (motorForce 2x lipat).
    /// Dipanggil oleh BoosterPickup ketika mobil mengambil booster.
    /// </summary>
    public void ActivateBoost(float duration)
    {
        // Jika boost sedang berjalan, restart timer-nya
        if (_boostCoroutine != null) StopCoroutine(_boostCoroutine);
        _boostCoroutine = StartCoroutine(BoostCoroutine(duration));
    }

    private IEnumerator BoostCoroutine(float duration)
    {
        motorForce = _defaultMotorForce * 2f;   // Gandakan gaya motor
        yield return new WaitForSeconds(duration);
        motorForce      = _defaultMotorForce;   // Kembalikan ke nilai normal
        _boostCoroutine = null;
    }

    // =========================
    // PUBLIC: INPUT LOCK
    // =========================
    /// <summary>
    /// Kunci atau lepas input pemain.
    /// Dipanggil GameHUD saat countdown berjalan dan saat pause.
    /// </summary>
    public void SetInputLocked(bool locked)
    {
        _inputLocked = locked;

        // Saat dikunci: paksa semua roda berhenti berputar dan terapkan rem penuh
        if (locked)
        {
            rearLeftWheelCollider.motorTorque   = 0f;
            rearRightWheelCollider.motorTorque  = 0f;
            frontLeftWheelCollider.brakeTorque  = brakeForce;
            frontRightWheelCollider.brakeTorque = brakeForce;
            rearLeftWheelCollider.brakeTorque   = brakeForce;
            rearRightWheelCollider.brakeTorque  = brakeForce;
        }
    }
}