using UnityEngine;

public class WheelSuspensionMover : MonoBehaviour
{
    [Header("References")]
    public WheelCollider wheelCollider;   // a kerék
    public Transform moveObject;          // amit fel-le mozgatunk (pl. vizuális rész)

    private float initialY;               // alap pozíció
    private float suspensionOffset;       // aktuális elmozdulás

    void Start()
    {
        if (moveObject)
            initialY = moveObject.localPosition.y; // alap magasság mentése
    }

    void Update()
    {
        if (!wheelCollider || !moveObject)
            return;

        WheelHit hit;
        if (wheelCollider.GetGroundHit(out hit))
        {
            // Számoljuk az aktuális rugó-összenyomódást
            float compression = wheelCollider.suspensionDistance - (hit.force / wheelCollider.suspensionSpring.spring);
            compression = Mathf.Clamp(compression, 0f, wheelCollider.suspensionDistance);

            // Az értéket Debug.Log-ban kiírjuk
            Debug.Log($"Suspension height: {compression:F3}");

            // Mozgatjuk a megadott objektumot a rugó értéke alapján
            Vector3 pos = moveObject.localPosition;
            pos.y = initialY - compression; // lefelé mozdul, ha összenyomódik
            moveObject.localPosition = pos;
        }
        else
        {
            // Ha nincs talaj alatt → rugó kinyújtva
            Vector3 pos = moveObject.localPosition;
            pos.y = initialY - wheelCollider.suspensionDistance;
            moveObject.localPosition = pos;

            Debug.Log($"Suspension height: {wheelCollider.suspensionDistance:F3} (no ground)");
        }
    }
}
