using Photon.Pun;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CarController))]
public class NetworkCar : MonoBehaviourPun, IPunObservable
{
    [Header("Interpolation")]
    public float interpolationDelay = 0.1f;

    [Header("Smoothing")]
    public float positionLerp = 0.25f;
    public float rotationLerp = 0.3f;

    [Header("Distances")]
    public float fastSyncDistance = 3f;
    public float teleportDistance = 10f;

    [Header("Buffer")]
    public int maxBufferSize = 12;

    private Rigidbody rb;
    private CarController carController;
    private readonly List<CarState> stateBuffer = new List<CarState>();

    private float targetSteerInput = 0f;
    private float smoothSteer = 0f;

    private Vector3 targetVelocity = Vector3.zero;
    private float targetAngularY = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        carController = GetComponent<CarController>();
    }

    void Start()
    {
        if (photonView.IsMine)
        {
            carController.enabled = true;
            rb.isKinematic = false;

            PhotonNetwork.SendRate = 20;
            PhotonNetwork.SerializationRate = 10;
        }
        else
        {
            carController.enabled = false;
            rb.isKinematic = false; // fontos a kerék forgáshoz
        }
    }

    void Update()
    {
        if (!photonView.IsMine)
        {
            float targetDelay = Mathf.Clamp(PhotonNetwork.GetPing() / 1000f * 1.2f, 0.08f, 0.15f);
            interpolationDelay = Mathf.Lerp(interpolationDelay, targetDelay, Time.deltaTime * 2f);

            if (stateBuffer.Count > maxBufferSize)
                stateBuffer.RemoveAt(0);
        }
    }

    void FixedUpdate()
    {
        if (photonView.IsMine)
        {
            carController.UpdateWheelVisuals();
        }
        else
        {
            ApplyInterpolatedState();

            // smooth kormányzás
            smoothSteer = Mathf.Lerp(smoothSteer, targetSteerInput, Time.fixedDeltaTime * 8f);
            float targetAngle = smoothSteer * carController.steerAngle;
            carController.SetSteerAngle(targetAngle);

            // kerekek forgása a hálózati állapot alapján
            UpdateWheelVisualsNetwork();

            // velocity simítás
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, 0.25f);
            rb.angularVelocity = new Vector3(rb.angularVelocity.x, targetAngularY, rb.angularVelocity.z);
        }
    }

    #region Wheel Visuals
    private void UpdateWheelVisualsNetwork()
    {
        if (carController == null) return;

        carController.UpdateWheel(carController.wheelFL, carController.wheelFLView);
        carController.UpdateWheel(carController.wheelFR, carController.wheelFRView);
        carController.UpdateWheel(carController.wheelRL, carController.wheelRLView);
        carController.UpdateWheel(carController.wheelRR, carController.wheelRRView);
    }
    #endregion

    #region Interpolation
    private void ApplyInterpolatedState()
    {
        if (stateBuffer.Count == 0)
            return;

        double renderTime = PhotonNetwork.Time - interpolationDelay;

        if (stateBuffer.Count == 1)
        {
            var s = stateBuffer[0];
            rb.position = s.position;
            rb.rotation = s.rotation;
            targetVelocity = s.velocity;
            targetAngularY = s.angularY;
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
            CarState latest = stateBuffer[stateBuffer.Count - 1];
            rb.position = Vector3.Lerp(rb.position, latest.position, positionLerp);
            rb.rotation = Quaternion.Slerp(rb.rotation, latest.rotation, rotationLerp);
            targetVelocity = latest.velocity;
            targetAngularY = latest.angularY;
            return;
        }

        if (newerIndex == 0)
        {
            var s = stateBuffer[0];
            rb.position = s.position;
            rb.rotation = s.rotation;
            targetVelocity = s.velocity;
            targetAngularY = s.angularY;
            return;
        }

        CarState newer = stateBuffer[newerIndex];
        CarState older = stateBuffer[newerIndex - 1];

        float t = 0f;
        if (newer.timestamp - older.timestamp > 0.0001f)
            t = Mathf.InverseLerp((float)older.timestamp, (float)newer.timestamp, (float)renderTime);

        Vector3 interpPos = Vector3.Lerp(older.position, newer.position, t);
        Quaternion interpRot = Quaternion.Slerp(older.rotation, newer.rotation, t);

        float dist = Vector3.Distance(rb.position, interpPos);

        if (dist > teleportDistance)
        {
            rb.position = interpPos;
            rb.rotation = interpRot;
        }
        else if (dist > fastSyncDistance)
        {
            rb.position = Vector3.Lerp(rb.position, interpPos, positionLerp * 2f);
            rb.rotation = Quaternion.Slerp(rb.rotation, interpRot, rotationLerp * 2f);
        }
        else
        {
            rb.position = Vector3.Lerp(rb.position, interpPos, positionLerp);
            rb.rotation = Quaternion.Slerp(rb.rotation, interpRot, rotationLerp);
        }

        targetVelocity = Vector3.Lerp(older.velocity, newer.velocity, t);
        targetAngularY = Mathf.Lerp(older.angularY, newer.angularY, t);
    }
    #endregion

    #region Photon Sync
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(rb.position);
            stream.SendNext(rb.rotation);
            stream.SendNext(rb.linearVelocity);
            stream.SendNext(rb.angularVelocity.y);
            stream.SendNext(carController.steeringInput);
            stream.SendNext(PhotonNetwork.Time);
        }
        else
        {
            CarState s = new CarState();
            s.position = (Vector3)stream.ReceiveNext();
            s.rotation = (Quaternion)stream.ReceiveNext();
            s.velocity = (Vector3)stream.ReceiveNext();
            s.angularY = (float)stream.ReceiveNext();
            s.steering = (float)stream.ReceiveNext();
            s.timestamp = (double)stream.ReceiveNext();

            targetSteerInput = s.steering;

            if (stateBuffer.Count == 0 || s.timestamp > stateBuffer[stateBuffer.Count - 1].timestamp)
                stateBuffer.Add(s);
            else
            {
                int insertIndex = stateBuffer.FindIndex(x => x.timestamp > s.timestamp);
                if (insertIndex == -1)
                    stateBuffer.Add(s);
                else
                    stateBuffer.Insert(insertIndex, s);
            }

            if (stateBuffer.Count > maxBufferSize)
                stateBuffer.RemoveRange(0, stateBuffer.Count - maxBufferSize);
        }
    }
    #endregion

    #region Struct
    public struct CarState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public float angularY;
        public float steering;
        public double timestamp;
    }
    #endregion
}
