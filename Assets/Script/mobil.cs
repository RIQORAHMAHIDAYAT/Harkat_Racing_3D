using UnityEngine;

public class mobil : MonoBehaviour, IVehicleController
{
    private float horizontalInput;
    private float verticalInput;
    private float currentSteerAngle;
    private float currentBrakeForce;
    private bool isBraking;

    // BOOST & INPUT LOCK
    private bool      _inputLocked       = false;
    private float     _defaultMotorForce = 0f;
    private Coroutine _boostCoroutine    = null;

    // WHEEL VISUAL SMOOTHING
    private Quaternion[] _wheelVisualRotations;
    private Vector3[] _wheelVisualPositions;

    // DRIFT STATE
    private float _driftFactor = 0f;
    private float _slideAngle = 0f;

    // SETTINGS
    [Header("Car Settings")]
    [SerializeField] private float motorForce = 2000f;
    [SerializeField] private float brakeForce = 5000f;
    [SerializeField] private float maxSteerAngle = 38f;
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.6f, 0f);

    [Header("Advanced Steering Settings")]
    [SerializeField] private float steerResponsiveSpeed = 360f;
    [SerializeField] private float steerCenterSpeed = 600f;
    [SerializeField] private float minSteerAngleAtHighSpeed = 26f;
    [SerializeField] private float speedForMinSteer = 120f;

    [Header("Drift / Friction Settings")]
    [SerializeField] private float frontWheelStiffness = 1.6f;
    [SerializeField] private float normalSidewaysStiffness = 1.8f;
    [SerializeField] private float driftSidewaysStiffness = 0.28f;
    [SerializeField] private float stiffnessTransitionSpeed = 4.0f;
    [SerializeField] private float driftTriggerThreshold = 0.75f;
    [SerializeField] private float driftSteerMultiplier = 1.3f;

    [Header("Physics Downforce")]
    [SerializeField] private float downforce = 150f;

    [Header("Drift Angle Assist")]
    [SerializeField] private float maxDriftAngle = 45f;
    [SerializeField] private float driftAngleRecoverySpeed = 3f;

    // WHEEL COLLIDERS
    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeftWheelCollider;
    [SerializeField] private WheelCollider frontRightWheelCollider;
    [SerializeField] private WheelCollider rearLeftWheelCollider;
    [SerializeField] private WheelCollider rearRightWheelCollider;

    // WHEEL VISUALS
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
            carRigidbody.centerOfMass = centerOfMassOffset;
            carRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        InitializeFrictionValues();
        _defaultMotorForce = motorForce;

        int wheelCount = 4;
        _wheelVisualRotations = new Quaternion[wheelCount];
        _wheelVisualPositions = new Vector3[wheelCount];
        Transform[] wheels = { frontLeftWheelTransform, frontRightWheelTransform, rearLeftWheelTransform, rearRightWheelTransform };
        for (int i = 0; i < wheelCount; i++)
        {
            if (wheels[i] != null)
            {
                _wheelVisualRotations[i] = wheels[i].rotation;
                _wheelVisualPositions[i] = wheels[i].position;
            }
        }
    }

    private void Update()
    {
        GetInput();
        UpdateWheels();
    }

    private void FixedUpdate()
    {
        HandleMotor();
        HandleSteering();
        HandleFriction();
        ApplyDownforce();
        UpdateDriftAngle();
    }

    private void InitializeFrictionValues()
    {
        ApplyStiffness(frontLeftWheelCollider, frontWheelStiffness);
        ApplyStiffness(frontRightWheelCollider, frontWheelStiffness);
        ApplyStiffness(rearLeftWheelCollider, normalSidewaysStiffness);
        ApplyStiffness(rearRightWheelCollider, normalSidewaysStiffness);
    }

    private void ApplyStiffness(WheelCollider wc, float stiffness)
    {
        WheelFrictionCurve f = wc.sidewaysFriction;
        f.stiffness = stiffness;
        wc.sidewaysFriction = f;
    }

    private void GetInput()
    {
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

    private void HandleMotor()
    {
        float speed = carRigidbody != null ? carRigidbody.linearVelocity.magnitude : 0f;

        if (Mathf.Abs(verticalInput) < 0.05f)
        {
            rearLeftWheelCollider.motorTorque = 0f;
            rearRightWheelCollider.motorTorque = 0f;

            if (speed < 0.15f)
            {
                ApplyAllBrakeTorque(15f);
                return;
            }
        }
        else
        {
            float torque = verticalInput * motorForce;

            if (speed > 5f)
            {
                float tractionFactor = 1f - _driftFactor * 0.4f;
                torque *= tractionFactor;
            }

            rearLeftWheelCollider.motorTorque = torque;
            rearRightWheelCollider.motorTorque = torque;
        }

        currentBrakeForce = isBraking ? brakeForce : 0f;
        ApplyBraking();
    }

    private void ApplyAllBrakeTorque(float torque)
    {
        frontLeftWheelCollider.brakeTorque = torque;
        frontRightWheelCollider.brakeTorque = torque;
        rearLeftWheelCollider.brakeTorque = torque;
        rearRightWheelCollider.brakeTorque = torque;
    }

    private void ApplyBraking()
    {
        frontLeftWheelCollider.brakeTorque = isBraking ? brakeForce * 0.15f : 0f;
        frontRightWheelCollider.brakeTorque = isBraking ? brakeForce * 0.15f : 0f;

        rearLeftWheelCollider.brakeTorque = currentBrakeForce;
        rearRightWheelCollider.brakeTorque = currentBrakeForce;
    }

    private void HandleSteering()
    {
        float speed = carRigidbody != null ? carRigidbody.linearVelocity.magnitude * 3.6f : 0f;

        float speedFactor = Mathf.Clamp01(speed / speedForMinSteer);
        float dynamicMaxSteer = Mathf.Lerp(maxSteerAngle, minSteerAngleAtHighSpeed, speedFactor);

        if (_driftFactor > 0.1f)
        {
            dynamicMaxSteer *= 1f + (_driftFactor * (driftSteerMultiplier - 1f));
        }

        float targetSteerAngle = dynamicMaxSteer * horizontalInput;

        float currentSteerSpeed = (horizontalInput == 0f) ? steerCenterSpeed : steerResponsiveSpeed;

        if (_driftFactor > 0.3f && horizontalInput != 0f)
        {
            currentSteerSpeed *= 1.5f;
        }

        currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteerAngle, Time.fixedDeltaTime * currentSteerSpeed);

        frontLeftWheelCollider.steerAngle = currentSteerAngle;
        frontRightWheelCollider.steerAngle = currentSteerAngle;
    }

    private void HandleFriction()
    {
        float speed = carRigidbody != null ? carRigidbody.linearVelocity.magnitude : 0f;
        float speedKmh = speed * 3.6f;

        float targetRearStiffness;

        if (isBraking)
        {
            targetRearStiffness = driftSidewaysStiffness;
        }
        else if (speedKmh > 30f && Mathf.Abs(horizontalInput) > 0.3f && Mathf.Abs(verticalInput) > 0.1f)
        {
            float inputBasedDrift = Mathf.Abs(horizontalInput) * (speedKmh / speedForMinSteer);
            inputBasedDrift = Mathf.Clamp01(inputBasedDrift);
            targetRearStiffness = Mathf.Lerp(normalSidewaysStiffness, driftSidewaysStiffness, inputBasedDrift * driftTriggerThreshold);
        }
        else
        {
            targetRearStiffness = normalSidewaysStiffness;
        }

        float currentStiffness = rearLeftWheelCollider.sidewaysFriction.stiffness;
        float newStiffness = Mathf.MoveTowards(currentStiffness, targetRearStiffness, Time.fixedDeltaTime * stiffnessTransitionSpeed);

        if (targetRearStiffness < normalSidewaysStiffness)
        {
            _driftFactor = 1f - ((newStiffness - driftSidewaysStiffness) / (normalSidewaysStiffness - driftSidewaysStiffness));
        }
        else
        {
            _driftFactor = 0f;
        }

        ApplyStiffness(rearLeftWheelCollider, newStiffness);
        ApplyStiffness(rearRightWheelCollider, newStiffness);
    }

    private void UpdateDriftAngle()
    {
        if (carRigidbody == null) return;

        Vector3 velocity = carRigidbody.linearVelocity;
        Vector3 forward = transform.forward;

        if (velocity.magnitude > 1f)
        {
            float angle = Vector3.SignedAngle(forward, velocity.normalized, Vector3.up);
            _slideAngle = Mathf.Lerp(_slideAngle, angle, Time.fixedDeltaTime * driftAngleRecoverySpeed);
        }
        else
        {
            _slideAngle = Mathf.Lerp(_slideAngle, 0f, Time.fixedDeltaTime * driftAngleRecoverySpeed * 2f);
        }

        _slideAngle = Mathf.Clamp(_slideAngle, -maxDriftAngle, maxDriftAngle);

        if (_driftFactor < 0.05f)
        {
            _slideAngle = Mathf.Lerp(_slideAngle, 0f, Time.fixedDeltaTime * driftAngleRecoverySpeed * 3f);
        }
    }

    private void ApplyDownforce()
    {
        if (carRigidbody != null)
        {
            float speed = carRigidbody.linearVelocity.magnitude;
            carRigidbody.AddForce(-transform.up * downforce * speed);
        }
    }

    public void ActivateBoost(float duration)
    {
        if (_boostCoroutine != null) StopCoroutine(_boostCoroutine);
        _boostCoroutine = StartCoroutine(BoostCoroutine(duration));
    }

    private System.Collections.IEnumerator BoostCoroutine(float duration)
    {
        motorForce = _defaultMotorForce * 2f;
        yield return new WaitForSeconds(duration);
        motorForce = _defaultMotorForce;
        _boostCoroutine = null;
    }

    public void SetInputLocked(bool locked)
    {
        Debug.Log($"[mobil] SetInputLocked({locked}) — pos={transform.position}");
        _inputLocked = locked;
        if (locked)
        {
            rearLeftWheelCollider.motorTorque = 0f;
            rearRightWheelCollider.motorTorque = 0f;
            ApplyAllBrakeTorque(brakeForce);
        }
    }

    private void UpdateWheels()
    {
        WheelCollider[] colliders = { frontLeftWheelCollider, frontRightWheelCollider, rearLeftWheelCollider, rearRightWheelCollider };
        Transform[] transforms = { frontLeftWheelTransform, frontRightWheelTransform, rearLeftWheelTransform, rearRightWheelTransform };

        for (int i = 0; i < 4; i++)
        {
            if (colliders[i] != null && transforms[i] != null)
            {
                UpdateSingleWheelSmooth(colliders[i], transforms[i], i);
            }
        }
    }

    private void UpdateSingleWheelSmooth(WheelCollider wheelCollider, Transform wheelTransform, int index)
    {
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);

        _wheelVisualPositions[index] = Vector3.Lerp(_wheelVisualPositions[index], pos, Time.deltaTime * 60f);

        string name = wheelTransform.name;
        bool isRight = name.Contains("kanan");

        if (isRight)
        {
            _wheelVisualRotations[index] = rot * Quaternion.Euler(0f, 180f, -90f);
        }
        else
        {
            _wheelVisualRotations[index] = rot * Quaternion.Euler(0f, 0f, 90f);
        }

        wheelTransform.position = _wheelVisualPositions[index];
        wheelTransform.rotation = _wheelVisualRotations[index];
    }
}
