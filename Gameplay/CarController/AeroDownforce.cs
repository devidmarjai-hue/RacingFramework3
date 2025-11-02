using UnityEngine;

public class AeroDownforce : MonoBehaviour
{
    [Header("References")]
    [Tooltip("A Rigidbody, amire az erő hatni fog. Ha üres, a komponens megpróbálja automatikusan megtalálni.")]
    public Rigidbody targetRigidbody;

    [Tooltip("A pont, ahol az erő alkalmazásra kerül. Ha üres, a jelenlegi GameObject pozícióját használja.")]
    public Transform applicationPoint;

    [Header("Downforce Settings")]
    [Tooltip("Leszorítóerő koefficiens. Nagyobb érték = nagyobb downforce.")]
    public float coefficient = 0.5f;

    [Tooltip("Sebességfüggés típusa. Ha igaz, akkor v^2 szerint nő (realistább).")]
    public bool useQuadratic = true;

    [Tooltip("Használja a helyi 'down' irányt (-transform.up). Ha hamis, globális Vector3.down.")]
    public bool useLocalDown = true;

    [Tooltip("Erő típus. Force = tömegfüggő, Acceleration = tömegtől független.")]
    public ForceMode forceMode = ForceMode.Force;

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color gizmoColor = Color.cyan;
    public float gizmoSize = 0.2f;

    private void Awake()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
            if (targetRigidbody == null)
            {
                Debug.LogWarning($"{name}: Nincs megadva Rigidbody, és a komponensen sem található!");
            }
        }

        if (applicationPoint == null)
            applicationPoint = transform;
    }

    private void FixedUpdate()
    {
        if (targetRigidbody == null) return;
        ApplyAeroDownforce();
    }

    private void ApplyAeroDownforce()
    {
        // aktuális sebesség (m/s)
        float speed = targetRigidbody.linearVelocity.magnitude;

        // erő kiszámítása (lineáris vagy négyzetes)
        float scale = useQuadratic ? speed * speed : speed;
        float downforce = coefficient * scale;

        // irány kiválasztása
        Vector3 downDir = useLocalDown ? -transform.up : Vector3.down;

        // erővektor
        Vector3 force = downDir.normalized * downforce;

        // alkalmazás a megadott ponton
        targetRigidbody.AddForceAtPosition(force, applicationPoint.position, forceMode);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || applicationPoint == null) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(applicationPoint.position, gizmoSize);

        if (Application.isPlaying && targetRigidbody != null)
        {
            float speed = targetRigidbody.linearVelocity.magnitude;
            float scale = useQuadratic ? speed * speed : speed;
            float downforce = coefficient * scale;
            Vector3 downDir = useLocalDown ? -transform.up : Vector3.down;
            Gizmos.DrawLine(applicationPoint.position, applicationPoint.position + downDir * downforce * 10000f);
        }
    }
}
