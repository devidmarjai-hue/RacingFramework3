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

    // tároljuk a távoli játékos kormányállását
    private float targetSteerInput = 0f;
    private float smoothSteer = 0f;

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
            rb.isKinematic = true;
        }
    }

    void Update()
    {
        if (!photonView.IsMine)
        {
            // Ping alapján adaptálódik az interpoláció
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

            // smooth kormány animáció (a steeringInput alapján)
            smoothSteer = Mathf.Lerp(smoothSteer, targetSteerInput, Time.fixedDeltaTime * 8f);
            carController.Steer(smoothSteer);

            carController.UpdateWheelVisuals();
        }
    }

    #region Interpolation

    private void ApplyInterpolatedState()
    {
        if (stateBuffer.Count == 0) return;
        double renderTime = PhotonNetwork.Time - interpolationDelay;

        if (stateBuffer.Count == 1)
        {
            var s = stateBuffer[0];
            rb.position = s.position;
            rb.rotation = s.rotation;
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
            return;
        }

        if (newerIndex == 0)
        {
            var s = stateBuffer[0];
            rb.position = s.position;
            rb.rotation = s.rotation;
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
    }

    #endregion

    #region Photon Sync

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Saját autó adatküldés
            stream.SendNext(rb.position);
            stream.SendNext(rb.rotation);
            stream.SendNext(carController.steeringInput); // ⬅️ Kormány vissza!
            stream.SendNext(PhotonNetwork.Time);
        }
        else
        {
            // Távoli autó adatfogadás
            CarState s = new CarState();
            s.position = (Vector3)stream.ReceiveNext();
            s.rotation = (Quaternion)stream.ReceiveNext();
            s.steering = (float)stream.ReceiveNext();
            s.timestamp = (double)stream.ReceiveNext();

            targetSteerInput = s.steering; // ⬅️ beállítjuk a hálózati kormányállást

            if (stateBuffer.Count > 0)
            {
                double dt = s.timestamp - stateBuffer[stateBuffer.Count - 1].timestamp;
                if (dt < 0.0001) s.timestamp = stateBuffer[stateBuffer.Count - 1].timestamp + 0.0001;
            }

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
        public float steering;
        public double timestamp;
    }

    #endregion
}
