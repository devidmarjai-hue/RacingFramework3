
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelDebugGizmo : MonoBehaviour
{
    public WheelCollider wheelFL;
    public WheelCollider wheelFR;
    public float gizmoLength = 0.5f;

    private void OnDrawGizmos()
    {
        if (wheelFL == null || wheelFR == null)
            return;

        DrawWheelDirection(wheelFL, Color.cyan);
        DrawWheelDirection(wheelFR, Color.magenta);
    }

    void DrawWheelDirection(WheelCollider wheel, Color color)
    {
        // Lekérjük a wheel world pozícióját és rotációját
        wheel.GetWorldPose(out Vector3 pos, out Quaternion rot);

        // A forward irány a kerék forgásirányát mutatja
        Vector3 forward = rot * Vector3.forward;

        // Gizmo rajzolása
        Gizmos.color = color;
        Gizmos.DrawSphere(pos, 0.05f); // kis pont a kerék középpontján
        Gizmos.DrawLine(pos, pos + forward * gizmoLength); // irányvonal
    }
}