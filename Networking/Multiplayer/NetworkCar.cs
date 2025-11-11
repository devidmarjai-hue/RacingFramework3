using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // <-- új Input System
using TMPro;

[RequireComponent(typeof(CarController))]
public class NetworkCar : MonoBehaviourPunCallbacks, IPunObservable
{
    TextMeshPro NickNameText;
    public CarController Car { get; private set; }
    public PhotonView PhotonView { get; private set; }
    public bool IsMine => PhotonView != null && PhotonView.IsMine;

    private readonly List<CarState> stateBuffer = new List<CarState>();
    private float targetSteerInput, smoothSteer;
    private float targetAccelInput, smoothAccel;
    private float targetBrakeInput, smoothBrake;

    private Rigidbody RB => Car.GetComponent<Rigidbody>();

    // Interpolation + smoothing settings
    private const float INTERPOLATION_DELAY = 0.12f;
    private const float POSITION_LERP = 0.25f;
    private const float ROTATION_LERP = 0.3f;
    private const float TELEPORT_DISTANCE = 10f;
    private const float FAST_SYNC_DISTANCE = 3f;
    private const int MAX_BUFFER_SIZE = 12;

    void Start()
    {
        PhotonView = GetComponent<PhotonView>();
        Car = GetComponent<CarController>();

        if (!PhotonNetwork.InRoom)
        {
            Destroy(this);
            return;
        }

        if (IsMine)
        {
            gameObject.AddComponent<AudioListener>();
        }


    }

    void FixedUpdate()
    {
        if (Car == null) return;

        if (IsMine)
        {
            Car.UpdateWheelVisuals();
        }
        else
        {
            ApplyInterpolatedState();

            smoothSteer = Mathf.Lerp(smoothSteer, targetSteerInput, Time.fixedDeltaTime * 8f);
            smoothAccel = Mathf.Lerp(smoothAccel, targetAccelInput, Time.fixedDeltaTime * 8f);
            smoothBrake = Mathf.Lerp(smoothBrake, targetBrakeInput, Time.fixedDeltaTime * 8f);

            Car.Steer(smoothSteer);
            Car.ApplyDrive(smoothAccel);
            Car.UpdateWheelVisuals();
        }
    }

    void Update()
    {
        // rotate nickname to face camera if visible
        if (NickNameText != null && NickNameText.gameObject.activeInHierarchy)
        {
            if (Camera.main != null)
                NickNameText.transform.rotation = Camera.main.transform.rotation;
        }

        // Új Input System használata: N billentyű
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.nKey.wasPressedThisFrame && NickNameText != null)
        {
            NickNameText.gameObject.SetActive(!NickNameText.gameObject.activeSelf);
        }
    }

    // --- Photon sync ---
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (Car == null) return;

        if (stream.IsWriting)
        {
            stream.SendNext(RB.position);
            stream.SendNext(RB.rotation);
            stream.SendNext(RB.velocity);
            stream.SendNext(RB.angularVelocity.y);
            stream.SendNext(Car.steeringInput);
            stream.SendNext(Car.throttleInput);
            stream.SendNext(Car.throttleInput < -0.1f ? 1f : 0f); // brake state
            stream.SendNext(PhotonNetwork.Time);
        }
        else
        {
            CarState s = new CarState();
            s.position = (Vector3)stream.ReceiveNext();
            s.rotation = (Quaternion)stream.ReceiveNext();
            s.velocity = (Vector3)stream.ReceiveNext();
            s.angularVelocityY = (float)stream.ReceiveNext();
            s.steer = (float)stream.ReceiveNext();
            s.accel = (float)stream.ReceiveNext();
            s.brake = (float)stream.ReceiveNext();
            s.timestamp = (double)stream.ReceiveNext();

            float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
            s.position += s.velocity * lag;
            s.rotation *= Quaternion.AngleAxis(s.angularVelocityY * lag, Vector3.up);

            targetSteerInput = s.steer;
            targetAccelInput = s.accel;
            targetBrakeInput = s.brake;

            if (stateBuffer.Count == 0 || s.timestamp > stateBuffer[stateBuffer.Count - 1].timestamp)
                stateBuffer.Add(s);

            if (stateBuffer.Count > MAX_BUFFER_SIZE)
                stateBuffer.RemoveRange(0, stateBuffer.Count - MAX_BUFFER_SIZE);
        }
    }

    // --- Interpolation ---
    private void ApplyInterpolatedState()
    {
        if (stateBuffer.Count == 0) return;
        double renderTime = PhotonNetwork.Time - INTERPOLATION_DELAY;

        if (stateBuffer.Count == 1)
        {
            ApplyState(stateBuffer[0]);
            return;
        }

        int newerIndex = -1;
        for (int i = 0; i < stateBuffer.Count; i++)
        {
            if (stateBuffer[i].timestamp > renderTime)
            {
                newerIndex = i;
                break;
            }
        }

        if (newerIndex == -1)
        {
            ApplyLerpedState(stateBuffer[stateBuffer.Count - 1]);
            return;
        }

        if (newerIndex == 0)
        {
            ApplyState(stateBuffer[0]);
            return;
        }

        CarState newer = stateBuffer[newerIndex];
        CarState older = stateBuffer[newerIndex - 1];

        float t = 0f;
        if (newer.timestamp - older.timestamp > 0.0001f)
            t = Mathf.InverseLerp((float)older.timestamp, (float)newer.timestamp, (float)renderTime);

        Vector3 interpPos = Vector3.Lerp(older.position, newer.position, t);
        Quaternion interpRot = Quaternion.Slerp(older.rotation, newer.rotation, t);
        Vector3 interpVel = Vector3.Lerp(older.velocity, newer.velocity, t);
        float interpAngVelY = Mathf.Lerp(older.angularVelocityY, newer.angularVelocityY, t);

        float dist = Vector3.Distance(RB.position, interpPos);

        if (dist > TELEPORT_DISTANCE)
        {
            RB.position = interpPos;
            RB.rotation = interpRot;
        }
        else if (dist > FAST_SYNC_DISTANCE)
        {
            RB.position = Vector3.Lerp(RB.position, interpPos, POSITION_LERP * 2f);
            RB.rotation = Quaternion.Slerp(RB.rotation, interpRot, ROTATION_LERP * 2f);
        }
        else
        {
            RB.position = Vector3.Lerp(RB.position, interpPos, POSITION_LERP);
            RB.rotation = Quaternion.Slerp(RB.rotation, interpRot, ROTATION_LERP);
        }

        RB.velocity = Vector3.Lerp(RB.velocity, interpVel, 0.25f);
        RB.angularVelocity = new Vector3(RB.angularVelocity.x, interpAngVelY, RB.angularVelocity.z);
    }

    private void ApplyState(CarState s)
    {
        RB.position = s.position;
        RB.rotation = s.rotation;
        RB.velocity = s.velocity;
        RB.angularVelocity = new Vector3(RB.angularVelocity.x, s.angularVelocityY, RB.angularVelocity.z);
    }

    private void ApplyLerpedState(CarState s)
    {
        RB.position = Vector3.Lerp(RB.position, s.position, POSITION_LERP);
        RB.rotation = Quaternion.Slerp(RB.rotation, s.rotation, ROTATION_LERP);
        RB.velocity = Vector3.Lerp(RB.velocity, s.velocity, 0.25f);
        RB.angularVelocity = new Vector3(RB.angularVelocity.x, s.angularVelocityY, RB.angularVelocity.z);
    }

    // --- State struct ---
    public struct CarState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public float angularVelocityY;
        public float steer;
        public float accel;
        public float brake;
        public double timestamp;
    }
}
