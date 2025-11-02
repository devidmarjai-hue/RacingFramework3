using UnityEngine;

public class SteeringWheelRotator : MonoBehaviour
{
    [Header("References")]
    public CarController car; // ide húzd be az autó CarController-jét

    [Header("Steering Wheel Settings")]
    public float maxWheelRotation = 360f;   // hány fokot fordulhat a kormány teljesen (pl. 540)
    public float smoothSpeed = 10f;         // forgatás simítása
    public Vector3 rotationAxis = new Vector3(0f, 0f, 1f); // melyik tengely mentén forogjon

    private float currentRotation;

    void Update()
    {
        if (car == null) return;

        // A steeringInput -1...+1 között van, így ebből kiszámoljuk a kormány elfordulását
        float targetRotation = car.steeringInput * maxWheelRotation;

        // simított forgás
        currentRotation = Mathf.Lerp(currentRotation, targetRotation, Time.deltaTime * smoothSpeed);

        // forgatás a megadott tengely mentén
        transform.localRotation = Quaternion.AngleAxis(currentRotation, rotationAxis);
    }
}
