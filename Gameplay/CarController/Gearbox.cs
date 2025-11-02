using System.Collections;
using UnityEngine;


[System.Serializable]
public class Gearbox : MonoBehaviour
{
    [Header("Settings")]
    public float[] gearRatios = new float[] { 3.6f, 2.65f, 2.15f, 1.78f, 1.52f, 1.34f, 1.20f };
    public float shiftDuration = 0.18f;

    [HideInInspector] public int currentGear = 0;
    [HideInInspector] public bool inGear = true;
    [HideInInspector] public float currentGearRatio = 1f;

    private bool shifting = false;

    public void Initialize()
    {
        currentGear = 0;
        inGear = true;
        currentGearRatio = gearRatios.Length > 0 ? gearRatios[currentGear] : 1f;
    }

    public void UpdatePhysics()
    {
        currentGearRatio = inGear && gearRatios.Length > 0 ? gearRatios[currentGear] : 0f;
    }

    public float GetDownstreamTorque(float inputTorque)
    {
        return inputTorque * currentGearRatio;
    }

    public float GetUpstreamAngularVelocity(float inputAngularVelocity)
    {
        return inputAngularVelocity * currentGearRatio;
    }

    public IEnumerator ShiftUp()
    {
        if (shifting || currentGear >= gearRatios.Length - 1) yield break;
        shifting = true; inGear = false;
        yield return new WaitForSeconds(shiftDuration);
        currentGear++; inGear = true; shifting = false;
    }

    public IEnumerator ShiftDown()
    {
        if (shifting || currentGear <= 0) yield break;
        shifting = true; inGear = false;
        yield return new WaitForSeconds(shiftDuration);
        currentGear--; inGear = true; shifting = false;
    }
}
