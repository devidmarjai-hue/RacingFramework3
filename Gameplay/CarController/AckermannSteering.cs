using UnityEngine;

public class AckermannSteering : MonoBehaviour
{
    public CarController car;
    public float wheelBase = 2.5f;
    public float trackWidth = 1.4f;

    void Update()
    {
        // Steering inputot a CarControllerből kérjük le
        float steerInput = car.steeringInput;

        float steerAngle = car.maxSteerAngle * steerInput;
        car.steerAngle = steerAngle; // fontos: carController ezt fogja használni

        if (Mathf.Abs(steerAngle) < 0.01f)
        {
            car.SetSteerAngle(0f);
            return;
        }

        float turnRadius = wheelBase / Mathf.Sin(Mathf.Deg2Rad * steerAngle);
        float innerAngle = Mathf.Rad2Deg * Mathf.Atan(wheelBase / (turnRadius + (trackWidth / 2)));
        float outerAngle = Mathf.Rad2Deg * Mathf.Atan(wheelBase / (turnRadius - (trackWidth / 2)));

        if (steerAngle > 0)
        {
            // jobbra fordul → jobb kerék belső
            car.wheelFL.steerAngle = outerAngle;
            car.wheelFR.steerAngle = innerAngle;
        }
        else
        {
            // balra fordul → bal kerék belső
            car.wheelFL.steerAngle = innerAngle;
            car.wheelFR.steerAngle = outerAngle;
        }
    }
}
