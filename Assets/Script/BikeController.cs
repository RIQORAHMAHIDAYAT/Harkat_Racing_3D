using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// BikeController — Mengontrol pergerakan sepeda motor menggunakan Rigidbody
/// dengan kestabilan penuh, perputaran setir, kemiringan bodi, dan penyesuaian collider.
/// </summary>
public class BikeController : MonoBehaviour, IVehicleController
{
    private float horizontalInput;
    private float verticalInput;
    private bool isBraking;

    private bool _inputLocked = false;
    private float _defaultMaxSpeed = 0f;
    private Coroutine _boostCoroutine = null;
    private bool _isBoosting = false;

    [Header("Bike Settings")]
    [SerializeField] private float maxSpeed = 25f;
    [SerializeField] private float acceleration = 15f;
    [SerializeField] private float deceleration = 10f;
    [SerializeField] private float turnSpeed = 100f;
    [SerializeField] private float maxSteerAngle = 35f;
    [SerializeField] private float steerResponseSpeed = 8f;
    [SerializeField] private float leanAngle = 28f;
    [SerializeField] private float leanSpeed = 6f;

    [Header("Physics Settings")]
    [SerializeField] private float extraGravity = 35f;
    [SerializeField] private float downforce = 12f;
    [SerializeField] private float groundCheckDistance = 1.4f;
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("Visual Parts (Optional, Auto-assigned)")]
    [SerializeField] private Transform handlebar;
    [SerializeField] private Transform frontWheel;
    [SerializeField] private Transform rearWheel;

    private Rigidbody rb;
    private Transform visualRoot;
    private float currentLean = 0f;
    private float currentSteerAngle = 0f;
    private bool isGrounded = false;
    private float frontWheelRotation = 0f;
    private float rearWheelRotation = 0f;

    [Header("Wall Avoidance & Recovery")]
    [SerializeField] private float wallPushForce = 20f;
    [SerializeField] private float wallLateralGripMultiplier = 0.25f;
    [SerializeField] private float minSteerSpeed = 0.1f;
    [SerializeField] private float lowSpeedSteerFactor = 0.5f;

    [Header("Respawn / Unstuck")]
    [SerializeField] private KeyCode respawnKey = KeyCode.R;
    [SerializeField] private float autoRespawnTime = 5f;
    [SerializeField] private float fallThresholdY = -3f;

    private Vector3 lastSafePosition;
    private Quaternion lastSafeRotation;
    private float stuckTimer = 0f;
    private bool isTouchingWall = false;
    private Vector3 wallContactNormal = Vector3.zero;
    private int wallContactCount = 0;
    private float respawnCooldown = 0f;
    private bool isRespawning = false;

    private void Awake()
    {
        // Ganti BoxCollider → CapsuleCollider agar tidak floating di slope.
        // Dilakukan di Awake() agar selesai sebelum script lain Start(),
        // sehingga GameHUD dan FinishLine mendeteksi collider yang benar.
        ReplaceBoxWithCapsuleCollider();
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        SetupVisualRoot();
        FindReferences();

        _defaultMaxSpeed = maxSpeed;

        lastSafePosition = transform.position;
        lastSafeRotation = transform.rotation;
    }

    private void ReplaceBoxWithCapsuleCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        PhysicsMaterial frictionlessMat = new PhysicsMaterial("Frictionless_Bike");
        frictionlessMat.dynamicFriction = 0f;
        frictionlessMat.staticFriction = 0f;
        frictionlessMat.frictionCombine = PhysicsMaterialCombine.Minimum;
        frictionlessMat.bounceCombine = PhysicsMaterialCombine.Minimum;

        float radius = Mathf.Max(box.size.x * 0.5f, 0.25f);
        Vector3 center = box.center;

        DestroyImmediate(box);

        CapsuleCollider cap = gameObject.AddComponent<CapsuleCollider>();
        cap.direction = 1;
        cap.radius = radius;
        cap.height = 0.7f;
        cap.center = new Vector3(center.x, 0.18f, center.z);
        cap.material = frictionlessMat;
    }

    private void SetupVisualRoot()
    {
        // Buat objek penampung baru di runtime
        GameObject rootObj = new GameObject("VisualRoot");
        rootObj.transform.SetParent(transform, false);
        visualRoot = rootObj.transform;
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        List<Transform> childrenToMove = new List<Transform>();
        foreach (Transform child in transform)
        {
            // Pindahkan semua bagian visual kecuali kamera penonton
            if (child != visualRoot && child.name != "TargetCamera")
            {
                childrenToMove.Add(child);
            }
        }

        foreach (Transform child in childrenToMove)
        {
            // Gunakan worldPositionStays = false agar koordinat lokal asli prefab tetap terjaga
            child.SetParent(visualRoot, false);
        }
    }

    private void FindReferences()
    {
        // Selalu cari dan gunakan pivot roda yang benar (axis___front_tire dan axis___rear_tire) 
        // untuk mencegah rotasi baling-baling/helikopter (orbital rotation) akibat salah mengambil locator_2/locator_1.
        Transform frontAxis = FindChildRecursive(transform, "axis___front_tire");
        if (frontAxis != null)
        {
            frontWheel = frontAxis;
        }
        else
        {
            frontWheel = FindChildRecursive(transform, "TIRE_FR");
        }
            
        Transform rearAxis = FindChildRecursive(transform, "axis___rear_tire");
        if (rearAxis != null)
        {
            rearWheel = rearAxis;
        }
        else
        {
            rearWheel = FindChildRecursive(transform, "TIRE_RR");
        }

        if (handlebar == null)
            handlebar = FindChildRecursive(transform, "C125_handle");
    }

    private Transform FindChildRecursive(Transform parent, string keyword)
    {
        if (parent.name.Contains(keyword)) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, keyword);
            if (found != null) return found;
        }
        return null;
    }

    private MeshFilter FindChildMeshFilter(Transform parent, string keyword)
    {
        Transform t = FindChildRecursive(parent, keyword);
        if (t != null) return t.GetComponent<MeshFilter>();
        return null;
    }

    private void AdjustColliderToWheelHeight()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return;

        // Cari sumbu roda untuk menentukan Z (wheelbase)
        Transform frontAxis = FindChildRecursive(transform, "axis___front_tire");
        Transform rearAxis = FindChildRecursive(transform, "axis___rear_tire");
        if (frontAxis == null) frontAxis = FindChildRecursive(transform, "TIRE_FR");
        if (rearAxis == null) rearAxis = FindChildRecursive(transform, "TIRE_RR");

        float frontLocalZ = 0.85f;
        float rearLocalZ = -0.72f;

        if (frontAxis != null)
            frontLocalZ = transform.InverseTransformPoint(frontAxis.position).z;
        if (rearAxis != null)
            rearLocalZ = transform.InverseTransformPoint(rearAxis.position).z;

        // Set collider Z persis mengikuti wheelbase — tanpa overhang
        float wheelSpan = Mathf.Abs(frontLocalZ - rearLocalZ);
        float newSizeZ = wheelSpan + 0.15f;
        float newCenterZ = (frontLocalZ + rearLocalZ) * 0.5f;
        col.size = new Vector3(col.size.x, col.size.y, newSizeZ);
        col.center = new Vector3(col.center.x, col.center.y, newCenterZ);

        // Cari MeshFilter ban untuk menentukan Y (ride height)
        MeshFilter frontMF = frontAxis != null ? frontAxis.GetComponentInChildren<MeshFilter>() : null;
        MeshFilter rearMF = rearAxis != null ? rearAxis.GetComponentInChildren<MeshFilter>() : null;
        if (frontMF == null) frontMF = FindChildMeshFilter(transform, "TIRE_FR");
        if (rearMF == null) rearMF = FindChildMeshFilter(transform, "TIRE_RR");

        float lowestTireY = float.MaxValue;

        System.Action<MeshFilter> processTire = (mf) =>
        {
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 localMin = mf.sharedMesh.bounds.min;
                Vector3 worldMin = mf.transform.TransformPoint(localMin);
                if (worldMin.y < lowestTireY)
                    lowestTireY = worldMin.y;
            }
        };

        processTire(frontMF);
        processTire(rearMF);

        if (lowestTireY != float.MaxValue)
        {
            float localLowY = transform.InverseTransformPoint(new Vector3(0, lowestTireY, 0)).y;
            float targetBottom = localLowY - 0.02f;
            float halfHeight = col.size.y * 0.5f;
            float newCenterY = targetBottom + halfHeight;
            col.center = new Vector3(col.center.x, newCenterY, col.center.z);
        }
        else
        {
            col.center = new Vector3(col.center.x, 0.22f, col.center.z);
        }
    }

    private void Update()
    {
        GetInput();
        UpdateAnimations();

        if (!_inputLocked && !isRespawning)
        {
            if (Input.GetKeyDown(respawnKey) && respawnCooldown <= 0f)
                StartCoroutine(RespawnRoutine());

            if (transform.position.y < fallThresholdY)
                StartCoroutine(RespawnRoutine());
        }

        if (respawnCooldown > 0f)
            respawnCooldown -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        isGrounded = CheckGrounded();

        if (_inputLocked)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * deceleration);
            }
            return;
        }

        HandleMovement();
    }

    private void GetInput()
    {
        if (_inputLocked)
        {
            horizontalInput = 0f;
            verticalInput = 0f;
            isBraking = false;
            return;
        }

        // Terapkan deadzone kecil (0.05) untuk mencegah controller drift/gerakan otomatis
        float rawHorizontal = Input.GetAxis("Horizontal");
        horizontalInput = (Mathf.Abs(rawHorizontal) > 0.05f) ? rawHorizontal : 0f;

        float rawVertical = Input.GetAxis("Vertical");
        verticalInput = (Mathf.Abs(rawVertical) > 0.05f) ? rawVertical : 0f;

        isBraking = Input.GetKey(KeyCode.Space);
    }

    private bool CheckGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, groundCheckDistance, groundLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform.root != transform.root && !hits[i].collider.isTrigger)
                return true;
        }
        return false;
    }

    private float SampleGroundAt(float zOffset)
    {
        Vector3 origin = transform.TransformPoint(new Vector3(0f, 0.45f, zOffset));
        RaycastHit hit;
        if (Physics.Raycast(origin, Vector3.down, out hit, groundCheckDistance + 0.5f, groundLayer))
        {
            if (hit.transform.root != transform.root && !hit.collider.isTrigger)
                return hit.point.y;
        }
        return float.MinValue;
    }

    private void HandleMovement()
    {
        if (rb == null) return;

        float speed = rb.linearVelocity.magnitude;

        if (isGrounded)
        {
            float targetSpeed = verticalInput * maxSpeed;
            Vector3 currentHorizontalVel = rb.linearVelocity;
            currentHorizontalVel.y = 0f;

            Vector3 targetVelocity = transform.forward * targetSpeed;
            Vector3 speedDiff = targetVelocity - currentHorizontalVel;

            float accelRate = (Mathf.Abs(targetSpeed) > 0.05f) ? acceleration : deceleration;
            rb.AddForce(speedDiff * accelRate, ForceMode.Acceleration);

            float gripFactor = isTouchingWall ? wallLateralGripMultiplier : 1f;
            Vector3 rightVel = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);
            rb.AddForce(-rightVel * 25f * gripFactor, ForceMode.Acceleration);

            if (isTouchingWall && wallContactCount > 0)
            {
                Vector3 pushDir = wallContactNormal + Vector3.up * 0.3f;
                pushDir.Normalize();
                rb.AddForce(pushDir * wallPushForce, ForceMode.Acceleration);
            }

            if (speed > minSteerSpeed)
            {
                float movingForward = Vector3.Dot(rb.linearVelocity, transform.forward) > 0f ? 1f : -1f;
                float rotAmount = horizontalInput * turnSpeed * movingForward * Time.fixedDeltaTime;
                transform.Rotate(0f, rotAmount, 0f);
            }
            else if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                float rotAmount = horizontalInput * turnSpeed * lowSpeedSteerFactor * Time.fixedDeltaTime;
                transform.Rotate(0f, rotAmount, 0f);
            }

            rb.AddForce(-transform.up * downforce * speed, ForceMode.Acceleration);

            if (speed < 0.8f)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > autoRespawnTime && !isRespawning)
                    StartCoroutine(RespawnRoutine());
            }
            else if (speed > 3f)
            {
                lastSafePosition = transform.position;
                lastSafeRotation = transform.rotation;
                stuckTimer = 0f;
            }
        }
        else
        {
            rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
        }
    }

    private void UpdateAnimations()
    {
        if (rb == null) return;

        float currentSpeed = rb.linearVelocity.magnitude;
        float movingForward = Vector3.Dot(rb.linearVelocity, transform.forward) >= 0f ? 1f : -1f;

        // 1. Putar roda (velg + ban) di sumbu X lokal secara presisi tanpa drift/wobble 
        // Menggunakan Quaternion.Euler(angle, 0, 0) mengunci rotasi Y & Z roda agar tidak oleng/goyang sama sekali.
        float rotationAmount = movingForward * currentSpeed * Time.deltaTime * (180f / (Mathf.PI * 0.35f));
        if (frontWheel != null)
        {
            frontWheelRotation = (frontWheelRotation + rotationAmount) % 360f;
            frontWheel.localRotation = Quaternion.Euler(frontWheelRotation, 0f, 0f);
        }
        if (rearWheel != null)
        {
            rearWheelRotation = (rearWheelRotation + rotationAmount) % 360f;
            rearWheel.localRotation = Quaternion.Euler(rearWheelRotation, 0f, 0f);
        }

        // 2. Putar handlebar (setang) secara lokal Y
        if (handlebar != null)
        {
            float targetSteer = horizontalInput * maxSteerAngle;
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteer, Time.deltaTime * steerResponseSpeed);
            handlebar.localRotation = Quaternion.Euler(0f, currentSteerAngle, 0f);
        }

        // 3. Miringkan seluruh bodi motor secara visual mengikuti slope (X = pitch, Z = lean)
        if (visualRoot != null)
        {
            float targetLean = -horizontalInput * leanAngle * Mathf.Clamp01(currentSpeed / 5f);
            currentLean = Mathf.Lerp(currentLean, targetLean, Time.deltaTime * leanSpeed);

            float slopePitch = 0f;
            float frontY = SampleGroundAt(0.85f);
            float rearY = SampleGroundAt(-0.75f);
            if (frontY > float.MinValue && rearY > float.MinValue)
            {
                float heightDiff = frontY - rearY;
                float distance = 1.6f;
                slopePitch = -Mathf.Atan2(heightDiff, distance) * Mathf.Rad2Deg;
                slopePitch = Mathf.Clamp(slopePitch, -25f, 25f);
            }

            visualRoot.localRotation = Quaternion.Euler(slopePitch, 0f, currentLean);
        }
    }

    // =========================================================
    // IVehicleController IMPLEMENTATION
    // =========================================================

    public void SetInputLocked(bool locked)
    {
        _inputLocked = locked;
        if (locked && rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void ActivateBoost(float duration)
    {
        if (_boostCoroutine != null) StopCoroutine(_boostCoroutine);
        _boostCoroutine = StartCoroutine(BoostCoroutine(duration));
    }

    private IEnumerator BoostCoroutine(float duration)
    {
        _isBoosting = true;
        maxSpeed = _defaultMaxSpeed * 1.8f;
        yield return new WaitForSeconds(duration);
        maxSpeed = _defaultMaxSpeed;
        _isBoosting = false;
        _boostCoroutine = null;
    }

    // =========================================================
    // COLLISION RESPONSE
    // =========================================================

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount > 0)
        {
            wallContactCount++;
            Vector3 avgNormal = Vector3.zero;
            foreach (ContactPoint cp in collision.contacts)
                avgNormal += cp.normal;
            wallContactNormal = (avgNormal / collision.contactCount).normalized;
            isTouchingWall = true;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.contactCount > 0)
        {
            Vector3 avgNormal = Vector3.zero;
            foreach (ContactPoint cp in collision.contacts)
                avgNormal += cp.normal;
            wallContactNormal = (avgNormal / collision.contactCount).normalized;
            isTouchingWall = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        wallContactCount--;
        if (wallContactCount <= 0)
        {
            wallContactCount = 0;
            isTouchingWall = false;
            wallContactNormal = Vector3.zero;
        }
    }

    // =========================================================
    // RESPAWN / UNSTUCK
    // =========================================================

    private IEnumerator RespawnRoutine()
    {
        if (isRespawning) yield break;
        isRespawning = true;

        // Reset velocity
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Flash effect via enabling/disabling renderer (opsional — fallback jika tidak ada)
        stuckTimer = 0f;
        respawnCooldown = 3f;

        // Jika belum pernah punya posisi aman, gunakan posisi sekarang
        if (lastSafePosition == Vector3.zero)
        {
            lastSafePosition = transform.position + Vector3.up * 1f;
            lastSafeRotation = transform.rotation;
        }

        // Pindahkan ke posisi aman
        transform.position = lastSafePosition + Vector3.up * 1.5f;
        transform.rotation = lastSafeRotation;

        yield return new WaitForFixedUpdate();

        isRespawning = false;
    }
}
