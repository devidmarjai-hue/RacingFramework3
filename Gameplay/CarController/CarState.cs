using UnityEngine;

[System.Serializable]
    // Hálózati állapot tárolása
    public class CarState
    {
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public float angularY;
    public double timestamp;
    public float steering; // ← új mező
    }