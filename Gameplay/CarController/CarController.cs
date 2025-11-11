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

[Header("Heave / Dual Damper Settings")]
public bool useRearHeaveDamper = true;
[Range(0f, 100000f)] public float rearHeaveStiffness = 8000f;    // spring N/m
[Range(0f, 20000f)] public float rearHeaveDamping = 2000f;       // damper N/(m/s)
public Transform rearHeaveForcePoint;                            // hol addja az erőt (hátsó kasztni pont)
public bool debugHeave = false;                                  // debug üzemmód

[Header("Slip Angle Compensation (ESC)")]
public bool useSlipAngleCompensation = true;
public float minSpeedForSlipComp = 5f;            // m/s alatt nem avatkozik be
public float slipDeadzoneDeg = 3f;               // kis szögeket figyelmen kívül hagy
public float maxActiveSlipDeg = 25f;             // ennél nagyobb csúszásnál nem avatkozik (pl. drift)
public float kp = 0.8f;                          // arányos erősége
public float kd = 0.4f;                          // derivatív (yaw csillapítás)
public float maxCorrectionTorque = 200f;         // Nm-ben limit
public float speedEffect = 0.02f;                // sebességszorzó
[Range(0f, 1f)]
public float slipCompIntensity = 0.6f;           // 0..1 skála, mennyire avatkozzon be
public bool debugSlipComp = false;




    [Header("UI")]
    public TextMeshProUGUI gearText;

    private Rigidbody rb;
    public float RPM;
    private float wheelRPM;
    //private float clutch = 1f;
    private float gearVelocity = 0f;
    private float currentGearFloat = 0f;

    [HideInInspector] public float throttleInput;
    [HideInInspector] public float steeringInput;
    [HideInInspector] public int currentGear = 0;
    [HideInInspector] public float steerAngle;

    private float _currentTorque;
    private float _oldRotation;
    private float _gearFactor;
    //private int _gearNum = 0;

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
		ApplyESC();
		//ApplySlipAngleCompensation();
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
        float speed = rb.linearVelocity.magnitude * 3.6f;

        int desiredGear = 0;
        for (int i = 0; i < gearSpeeds.Length; i++)
            if (speed >= gearSpeeds[i]) desiredGear = i;

        float effectiveSmooth = desiredGear > currentGear
            ? shiftSmoothTime * (1f + throttleInput * upshiftAggressionMultiplier)
            : downshiftSmoothTime;

        currentGearFloat = Mathf.SmoothDamp(currentGearFloat, desiredGear, ref gearVelocity, effectiveSmooth, Mathf.Infinity, Time.fixedDeltaTime);
        currentGear = Mathf.Clamp(Mathf.RoundToInt(currentGearFloat), 0, gearSpeeds.Length - 1);
    }

public void ApplyDrive(float accel)
{
    float speed = rb.linearVelocity.magnitude;
    float speedKmh = speed * 3.6f;
    float thrustTorque = 0f;

    // --- Paraméterek ---
    float brakeStrength = _fullTorqueOverAllWheels * 8.0f; 
    float frontBias = 0.7f;
    float rearBias = 1f - frontBias;
    float engineBrake = 0.05f * _fullTorqueOverAllWheels;
    float maxReverseSpeedKmh = 40f; // hátrameneti sebességkorlát

    // --- Fékek alaphelyzetbe ---
    wheelFL.brakeTorque = wheelFR.brakeTorque = wheelRL.brakeTorque = wheelRR.brakeTorque = 0f;

    // --- Hátramenet mód detektálás ---
    bool isReversing = Vector3.Dot(rb.linearVelocity, transform.forward) < -0.5f; // ha hátrafelé mozgunk
    bool canReverse = accel < -0.1f && (speedKmh < 2f || isReversing);

    // --- HAJTÁS LOGIKA ---
    if (accel > 0.1f)
    {
        // Előremenet
        float driveInput = accel;
        switch (_carDriveType)
        {
            case CarDriveType.FrontWheelDrive:
                thrustTorque = driveInput * (_currentTorque / 2f);
                wheelFL.motorTorque = wheelFR.motorTorque = thrustTorque;
                wheelRL.motorTorque = wheelRR.motorTorque = 0f;
                break;

            case CarDriveType.RearWheelDrive:
                thrustTorque = driveInput * (_currentTorque / 2f);
                wheelRL.motorTorque = wheelRR.motorTorque = thrustTorque;
                wheelFL.motorTorque = wheelFR.motorTorque = 0f;
                break;

            case CarDriveType.AllWheelDrive:
                thrustTorque = driveInput * (_currentTorque / 4f);
                wheelFL.motorTorque = wheelFR.motorTorque = wheelRL.motorTorque = wheelRR.motorTorque = thrustTorque;
                break;
        }
    }
    else if (canReverse)
    {
        // --- HÁTRAMENET HAJTÁS ---
        if (speedKmh < maxReverseSpeedKmh)
        {
            float reverseInput = Mathf.Abs(accel);
            switch (_carDriveType)
            {
                case CarDriveType.FrontWheelDrive:
                    thrustTorque = reverseInput * (_currentTorque / 2f);
                    wheelFL.motorTorque = wheelFR.motorTorque = -thrustTorque;
                    wheelRL.motorTorque = wheelRR.motorTorque = 0f;
                    break;

                case CarDriveType.RearWheelDrive:
                    thrustTorque = reverseInput * (_currentTorque / 2f);
                    wheelRL.motorTorque = wheelRR.motorTorque = -thrustTorque;
                    wheelFL.motorTorque = wheelFR.motorTorque = 0f;
                    break;

                case CarDriveType.AllWheelDrive:
                    thrustTorque = reverseInput * (_currentTorque / 4f);
                    wheelFL.motorTorque = wheelFR.motorTorque = wheelRL.motorTorque = wheelRR.motorTorque = -thrustTorque;
                    break;
            }
        }
        else
        {
            // ha túl gyors hátrafelé, motorfék
            wheelFL.brakeTorque = wheelFR.brakeTorque = wheelRL.brakeTorque = wheelRR.brakeTorque = engineBrake * 3f;
        }
    }

    // --- FÉKEZÉS ---
    if (accel < -0.1f && !canReverse)
    {
        float brake = Mathf.Abs(accel) * brakeStrength;

        // motor nyomatékot vedd el fékezésnél
        wheelFL.motorTorque = wheelFR.motorTorque = wheelRL.motorTorque = wheelRR.motorTorque = 0f;

        // fék elosztás
        wheelFL.brakeTorque = wheelFR.brakeTorque = brake * frontBias;
        wheelRL.brakeTorque = wheelRR.brakeTorque = brake * rearBias;

        ApplyABS();
    }
    else if (Mathf.Abs(accel) < 0.1f)
    {
        // motorfék (felengedett gáz)
        wheelFL.brakeTorque = wheelFR.brakeTorque = wheelRL.brakeTorque = wheelRR.brakeTorque = engineBrake;
    }
}

void ApplyESC()
{
   if (!useRearHeaveDamper || rearHeaveForcePoint == null) return;

    WheelHit hit;
    float travelL = 0f, travelR = 0f;
    bool groundedL = wheelRL.GetGroundHit(out hit);
    if (groundedL)
        travelL = (wheelRL.transform.InverseTransformPoint(hit.point).y - wheelRL.radius) / wheelRL.suspensionDistance;

    bool groundedR = wheelRR.GetGroundHit(out hit);
    if (groundedR)
        travelR = (wheelRR.transform.InverseTransformPoint(hit.point).y - wheelRR.radius) / wheelRR.suspensionDistance;

    // Átlagolt travel (csak ha legalább egy kerék érintkezik)
    float avgTravel = 0f;
    int groundedCount = 0;
    if (groundedL) { avgTravel += travelL; groundedCount++; }
    if (groundedR) { avgTravel += travelR; groundedCount++; }
    if (groundedCount == 0) return;

    avgTravel /= groundedCount;

    // Travel referencia (0 = szabad állás)
    float desiredTravel = 0f;
    float travelError = avgTravel - desiredTravel;

    // Lokális függőleges sebesség
    Vector3 localVel = transform.InverseTransformDirection(rb.GetPointVelocity(rearHeaveForcePoint.position));
    float verticalVel = localVel.y;

    // Rugó + csillapítás erő
    float springForce = -rearHeaveStiffness * travelError;
    float damperForce = -rearHeaveDamping * verticalVel;
    float totalForce = springForce + damperForce;

    // Limitáljuk, hogy ne dobja el a kasztnit
    totalForce = Mathf.Clamp(totalForce, -20000f, 20000f);

    // Alkalmazzuk lefelé mutató erőként
    rb.AddForceAtPosition(-transform.up * totalForce, rearHeaveForcePoint.position);

    // Debug
    if (debugHeave)
    {
        Debug.DrawLine(rearHeaveForcePoint.position, rearHeaveForcePoint.position + (-transform.up * totalForce * 0.0005f), Color.cyan);
        Debug.Log($"Heave | Travel: {avgTravel:F3} | Error: {travelError:F3} | vVel: {verticalVel:F2} | F={totalForce:F1}");
    }
}


// --- ABS rendszer ---
void ApplyABS()
{
    WheelHit hit;
    float slipThreshold = 0.6f;   // ha ennél jobban csúszik, csökkentse a féket
    float absStrength = 0.6f;     // mennyire csökkentse a fékerőt

    WheelCollider[] wheels = { wheelFL, wheelFR, wheelRL, wheelRR };

    foreach (var wheel in wheels)
    {
        if (wheel.GetGroundHit(out hit))
        {
            if (Mathf.Abs(hit.forwardSlip) > slipThreshold)
            {
                // Csökkenti a fékerőt, hogy ne blokkoljon a kerék
                wheel.brakeTorque *= absStrength;
            }
        }
    }
}


void ApplySlipAngleCompensation()
{
    if (!useSlipAngleCompensation) return;

    float speed = rb.velocity.magnitude;
    if (speed < minSpeedForSlipComp) return;

    // irányok
    Vector3 forward = transform.forward;
    Vector3 velocityDir = rb.velocity.sqrMagnitude > 0.0001f ? rb.velocity.normalized : forward;

    // slip szög (deg)
    float slipAngle = Vector3.SignedAngle(forward, velocityDir, Vector3.up);

    // kis eltérések ignorálása
    if (Mathf.Abs(slipAngle) < slipDeadzoneDeg) return;

    // drift közben ne avatkozzon (engedjük, hogy a játékos kezelje)
    if (Mathf.Abs(slipAngle) > maxActiveSlipDeg) return;

    // PD szabályozás (stabilizáló)
    float pTerm = kp * slipAngle;
    float yawRateDeg = rb.angularVelocity.y * Mathf.Rad2Deg;
    float dTerm = kd * yawRateDeg;

    // sebességskálázás
    float speedFactor = 1f + speed * speedEffect;

    // kontroll jel - a cél: csillapítani, nem "ellentekerni"
    float control = -(pTerm + dTerm) * slipCompIntensity * speedFactor;

    // torque korlátozás
    float torque = Mathf.Clamp(control, -maxCorrectionTorque, maxCorrectionTorque);

    // alkalmazás relatív tengely mentén
    rb.AddRelativeTorque(Vector3.up * torque, ForceMode.Force);

    if (debugSlipComp)
    {
        Debug.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * 2f, Color.green);
        Debug.DrawRay(transform.position + Vector3.up * 0.5f, velocityDir * 2f, Color.yellow);
        Debug.Log($"SlipComp | slip:{slipAngle:F2}°  p:{pTerm:F2}  d:{dTerm:F2}  speed:{speed:F2}  torque:{torque:F1}");
    }
}



    public void Steer(float steerInput)
    {
        float speed = rb.linearVelocity.magnitude * 3.6f; // m/s → km/h
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

        gearText.text = throttleInput < -0.1f && rb.linearVelocity.magnitude * 3.6f < reverseEngageSpeed
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
        rb.AddForce(_downForce * rb.linearVelocity.magnitude * -transform.up);
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