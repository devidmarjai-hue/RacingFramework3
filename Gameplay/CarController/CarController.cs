using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public enum CarDriveType
{
    FrontWheelDrive,
    RearWheelDrive,
    AllWheelDrive
}

public enum SpeedType
{
    MPH,
    KPH
}

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Drive Settings")]
    public CarDriveType _carDriveType = CarDriveType.AllWheelDrive;
    public SpeedType _speedType = SpeedType.MPH;

    [Header("Car Settings")]
    public float maxSteerAngle = 25f;
    public float redLine = 18000f;
    public float idleRPM = 4000f;
    public float motorForce = 1500f;
    public float _fullTorqueOverAllWheels = 1500f;
    public float _reverseTorque = 500f;
    public float _brakeTorque = 1500f;
    public float _maxHandbrakeTorque = 10000f;
    public float _slipLimit = 0.5f;
    public float _downForce = 100f;
    public float _antiRollVal = 3500f;
    public float _steerHelper = 0.8f;
    public float _tractionControl = 0.8f;

    [Header("Automatic Gear Settings (km/h)")]
    public float[] gearSpeeds = { 0f, 60f, 100f, 140f, 180f, 230f, 280f, 320f };
    public float shiftSmoothTime = 0.25f;
    public float downshiftSmoothTime = 0.05f;
    public float upshiftAggressionMultiplier = 1.5f;

    [Header("Gearbox Ratios")]
    public float[] gearRatios = { 3.0f, 2.2f, 1.8f, 1.5f, 1.3f, 1.1f, 0.9f };
    public float differentialRatio = 3.42f;
    public float reverseGearRatio = -2.5f;
    public float reverseEngageSpeed = 2f;

    [Header("Torque Curve")]
    public AnimationCurve torqueCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 200f),
        new Keyframe(0.4f, 400f),
        new Keyframe(0.6f, 550f),
        new Keyframe(0.8f, 650f),
        new Keyframe(1f, 600f)
    );

    [Header("References")]
    public Transform centerOfMassTransform;
    public WheelCollider wheelFL;
    public WheelCollider wheelFR;
    public WheelCollider wheelRL;
    public WheelCollider wheelRR;

    [Header("Wheel Visuals")]
    public Transform wheelFLView;
    public Transform wheelFRView;
    public Transform wheelRLView;
    public Transform wheelRRView;

    [Header("Steering Mode")]
    public bool useAckermann = true;
    public float wheelBase = 2.6f;
    public float trackWidth = 1.6f;

    [Header("Steering Dynamics")]
    public float maxSpeed = 120f;
    public float steerLerpSpeed = 6f;
    public float returnLerpSpeed = 5f;
    public float directionChangeBoost = 2f;
    public float autoCenterStrength = 3f;

    [Header("UI")]
    public TextMeshProUGUI gearText;

    private Rigidbody rb;
    public float RPM;
    private float wheelRPM;
    private float clutch = 1f;
    private float gearVelocity = 0f;
    private float currentGearFloat = 0f;

    [HideInInspector] public float throttleInput;
    [HideInInspector] public float steeringInput;
    [HideInInspector] public int currentGear = 0;
    [HideInInspector] public float steerAngle;

    private float _currentTorque;
    private float _oldRotation;
    private float _gearFactor;
    private int _gearNum = 0;

    // steering internal vars
    private float currentSteer;
    private float lastSteerInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (centerOfMassTransform != null)
            rb.centerOfMass = transform.InverseTransformPoint(centerOfMassTransform.position);

        _currentTorque = _fullTorqueOverAllWheels - (_tractionControl * _fullTorqueOverAllWheels);
    }

    void Update()
    {
        if (!enabled) return;

        float forward = 0f;
        float turn = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) forward += 1f;
            if (Keyboard.current.sKey.isPressed) forward -= 1f;
            if (Keyboard.current.aKey.isPressed) turn -= 1f;
            if (Keyboard.current.dKey.isPressed) turn += 1f;

            if (Keyboard.current.upArrowKey.isPressed) forward += 1f;
            if (Keyboard.current.downArrowKey.isPressed) forward -= 1f;
            if (Keyboard.current.leftArrowKey.isPressed) turn -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed) turn += 1f;
        }

        if (Gamepad.current != null)
        {
            forward += Gamepad.current.leftStick.y.ReadValue();
            turn += Gamepad.current.leftStick.x.ReadValue();
        }

        throttleInput = Mathf.Clamp(forward, -1f, 1f);
        steeringInput = Mathf.Clamp(turn, -1f, 1f);

        if (Mathf.Abs(throttleInput) < 0.05f) throttleInput = 0f;
    }

    void FixedUpdate()
    {
        if (!enabled) return;

        UpdateRPM();
        AutomaticGearbox();
        ApplyDrive(throttleInput);
        Steer(steeringInput);
        UpdateWheelVisuals();
        UpdateGearUI();
        AntiRoll();
        AddDownForce();
        TractionControl();
    }

    void UpdateRPM()
    {
        wheelRPM = ((wheelRL?.rpm ?? 0f) + (wheelRR?.rpm ?? 0f)) * 0.5f;
        wheelRPM = Mathf.Max(0f, wheelRPM);

        float targetRPM = Mathf.Max(idleRPM, wheelRPM * gearRatios[currentGear] * differentialRatio);
        RPM = Mathf.Lerp(RPM, targetRPM, Time.fixedDeltaTime * 5f);
    }



    void AutomaticGearbox()
    {
        float speed = rb.velocity.magnitude * 3.6f;

        int desiredGear = 0;
        for (int i = 0; i < gearSpeeds.Length; i++)
            if (speed >= gearSpeeds[i]) desiredGear = i;

        float effectiveSmooth = desiredGear > currentGear
            ? shiftSmoothTime * (1f + throttleInput * upshiftAggressionMultiplier)
            : downshiftSmoothTime;

        currentGearFloat = Mathf.SmoothDamp(currentGearFloat, desiredGear, ref gearVelocity, effectiveSmooth, Mathf.Infinity, Time.fixedDeltaTime);
        currentGear = Mathf.Clamp(Mathf.RoundToInt(currentGearFloat), 0, gearSpeeds.Length - 1);
    }

    void ApplyDrive(float accel)
    {
        float speed = rb.velocity.magnitude;
        float thrustTorque;

        switch (_carDriveType)
        {
            case CarDriveType.FrontWheelDrive:
                thrustTorque = accel * (_currentTorque / 2f);
                wheelFL.motorTorque = wheelFR.motorTorque = thrustTorque;
                break;
            case CarDriveType.RearWheelDrive:
                thrustTorque = accel * (_currentTorque / 2f);
                wheelRL.motorTorque = wheelRR.motorTorque = thrustTorque;
                break;
            case CarDriveType.AllWheelDrive:
                thrustTorque = accel * (_currentTorque / 4f);
                wheelFL.motorTorque = wheelFR.motorTorque = wheelRL.motorTorque = wheelRR.motorTorque = thrustTorque;
                break;
        }

        // Motorfék
    // --- SEBESSÉG + FORDULATSZÁM alapú motorfék számítás ---
    float gearFactor = Mathf.InverseLerp(0, gearRatios.Length - 1, currentGear);         // alacsony fokozat -> nagyobb motorfék
    float rpmFactor = Mathf.InverseLerp(idleRPM, redLine, RPM);                          // magasabb RPM -> nagyobb motorfék
    float engineBrake = _fullTorqueOverAllWheels * Mathf.Lerp(0.025f, 0.2f, rpmFactor * (1f - gearFactor));

    // --- Cél fékerő ---
    float targetBrake = (Mathf.Abs(accel) < 0.1f) ? engineBrake : 0f;

    // --- Simítás (ne harapjon be hirtelen) ---
    float smoothBrake = Mathf.Lerp(
        wheelFL.brakeTorque,
        targetBrake,
        Time.fixedDeltaTime * 1f
    );

        if (Mathf.Abs(accel) < 0.1f)
        {
            wheelFL.motorTorque = wheelFR.motorTorque = wheelRL.motorTorque = wheelRR.motorTorque = 0f;
            wheelFL.brakeTorque = wheelFR.brakeTorque = wheelRL.brakeTorque = wheelRR.brakeTorque = engineBrake;
        }
        else
        {
            wheelFL.brakeTorque = wheelFR.brakeTorque = wheelRL.brakeTorque = wheelRR.brakeTorque = 0f;
        }
    }

    public void Steer(float steerInput)
    {
        float speed = rb.velocity.magnitude * 3.6f; // m/s → km/h
        float speedFactor = Mathf.Clamp01(speed / maxSpeed);
        float dynamicMaxSteer = Mathf.Lerp(maxSteerAngle, maxSteerAngle * 0.3f, speedFactor);
        float targetSteer = dynamicMaxSteer * steerInput;

        bool directionChanged = Mathf.Sign(steerInput) != Mathf.Sign(lastSteerInput) && Mathf.Abs(steerInput) > 0.2f;

        float currentLerpSpeed = steerLerpSpeed;
        if (directionChanged)
            currentLerpSpeed *= directionChangeBoost;

        if (Mathf.Abs(steerInput) < 0.01f)
            currentLerpSpeed = returnLerpSpeed + autoCenterStrength;

        currentSteer = Mathf.Lerp(currentSteer, targetSteer, Time.fixedDeltaTime * currentLerpSpeed);
        steerAngle = currentSteer;

        if (!useAckermann)
        {
            wheelFL.steerAngle = steerAngle;
            wheelFR.steerAngle = steerAngle;
        }
        else
        {
            float angleRad = Mathf.Abs(steerAngle) * Mathf.Deg2Rad;
            float radius = wheelBase / Mathf.Sin(Mathf.Max(angleRad, 0.001f));
            float inner = Mathf.Rad2Deg * Mathf.Atan(wheelBase / (radius - (trackWidth * 0.5f)));
            float outer = Mathf.Rad2Deg * Mathf.Atan(wheelBase / (radius + (trackWidth * 0.5f)));

            float fl = (steerAngle > 0f) ? outer : inner;
            float fr = (steerAngle > 0f) ? inner : outer;

            wheelFL.steerAngle = fl * Mathf.Sign(steerAngle);
            wheelFR.steerAngle = fr * Mathf.Sign(steerAngle);
        }

        lastSteerInput = steerInput;
    }

    public void SetSteerAngle(float angle)
    {
        wheelFL.steerAngle = angle;
        wheelFR.steerAngle = angle;
    }
    public void UpdateWheelVisuals()
    {
        UpdateWheel(wheelFL, wheelFLView);
        UpdateWheel(wheelFR, wheelFRView);
        UpdateWheel(wheelRL, wheelRLView);
        UpdateWheel(wheelRR, wheelRRView);
    }

    public void UpdateWheel(WheelCollider col, Transform wheelTrans)
    {
        if (wheelTrans == null || col == null) return;
        Vector3 pos;
        Quaternion rot;
        col.GetWorldPose(out pos, out rot);
        wheelTrans.position = pos;
        wheelTrans.rotation = rot;
    }

    void UpdateGearUI()
    {
        if (!gearText) return;

        gearText.text = throttleInput < -0.1f && rb.velocity.magnitude * 3.6f < reverseEngageSpeed
            ? "Gear: R"
            : "Gear: " + (currentGear + 1);
    }

    void AntiRoll()
    {
        WheelHit hit;
        float travelL = 1f, travelR = 1f;

        bool groundedLf = wheelFL.GetGroundHit(out hit);
        if (groundedLf) travelL = (-wheelFL.transform.InverseTransformPoint(hit.point).y - wheelFL.radius) / wheelFL.suspensionDistance;

        bool groundedRf = wheelFR.GetGroundHit(out hit);
        if (groundedRf) travelR = (-wheelFR.transform.InverseTransformPoint(hit.point).y - wheelFR.radius) / wheelFR.suspensionDistance;

        float antiRollForce = (travelL - travelR) * _antiRollVal;
        if (groundedLf) rb.AddForceAtPosition(wheelFL.transform.up * -antiRollForce, wheelFL.transform.position);
        if (groundedRf) rb.AddForceAtPosition(wheelFR.transform.up * antiRollForce, wheelFR.transform.position);

        bool groundedLr = wheelRL.GetGroundHit(out hit);
        if (groundedLr) travelL = (-wheelRL.transform.InverseTransformPoint(hit.point).y - wheelRL.radius) / wheelRL.suspensionDistance;

        bool groundedRr = wheelRR.GetGroundHit(out hit);
        if (groundedRr) travelR = (-wheelRR.transform.InverseTransformPoint(hit.point).y - wheelRR.radius) / wheelRR.suspensionDistance;

        antiRollForce = (travelL - travelR) * _antiRollVal;
        if (groundedLr) rb.AddForceAtPosition(wheelRL.transform.up * -antiRollForce, wheelRL.transform.position);
        if (groundedRr) rb.AddForceAtPosition(wheelRR.transform.up * antiRollForce, wheelRR.transform.position);
    }

    void AddDownForce()
    {
        rb.AddForce(_downForce * rb.velocity.magnitude * -transform.up);
    }

    void TractionControl()
    {
        WheelHit wheelHit;
        switch (_carDriveType)
        {
            case CarDriveType.FrontWheelDrive:
                wheelFL.GetGroundHit(out wheelHit); AdjustTorque(wheelHit.forwardSlip);
                wheelFR.GetGroundHit(out wheelHit); AdjustTorque(wheelHit.forwardSlip);
                break;
            case CarDriveType.RearWheelDrive:
                wheelRL.GetGroundHit(out wheelHit); AdjustTorque(wheelHit.forwardSlip);
                wheelRR.GetGroundHit(out wheelHit); AdjustTorque(wheelHit.forwardSlip);
                break;
            case CarDriveType.AllWheelDrive:
                wheelFL.GetGroundHit(out wheelHit); AdjustTorque(wheelHit.forwardSlip);
                wheelFR.GetGroundHit(out wheelHit); AdjustTorque(wheelHit.forwardSlip);
                wheelRL.GetGroundHit(out wheelHit); AdjustTorque(wheelHit.forwardSlip);
                wheelRR.GetGroundHit(out wheelHit); AdjustTorque(wheelHit.forwardSlip);
                break;
        }
    }

    void AdjustTorque(float forwardSlip)
    {
        if (forwardSlip >= _slipLimit && _currentTorque >= 0)
        {
            _currentTorque -= 10 * _tractionControl;
        }
        else
        {
            _currentTorque += 10 * _tractionControl;
            if (_currentTorque > _fullTorqueOverAllWheels)
                _currentTorque = _fullTorqueOverAllWheels;
        }
    }
	
	
	
}
