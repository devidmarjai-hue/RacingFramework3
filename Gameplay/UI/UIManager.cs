using UnityEngine;
using TMPro;
using Photon.Pun;

public class UIManager : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI gearTextUI;
    public TextMeshProUGUI rpmTextUI;
    public TextMeshProUGUI speedTextUI; // <-- ide írjuk a sebességet

    private Rigidbody carRb;
    private CarController car;

    void Start()
    {
        AssignLocalPlayer();
    }

    void Update()
    {
        if (carRb == null || car == null)
        {
            AssignLocalPlayer();
            if (carRb == null) return; // Még nincs helyi player
        }

        // Sebesség km/h-ban
        float speedKMH = carRb.linearVelocity.magnitude * 3.6f;

        // Speed megjelenítése
        if (speedTextUI != null)
        {
            speedTextUI.text = Mathf.RoundToInt(speedKMH) + " km/h";
        }

        // Gear megjelenítése
        if (gearTextUI != null)
        {
            if (car.throttleInput < -0.1f && speedKMH < car.reverseEngageSpeed)
                gearTextUI.text = "Gear: R";
            else
                gearTextUI.text = "Gear: " + (car.currentGear + 1);
        }

        // RPM megjelenítése
        if (rpmTextUI != null)
        {
            rpmTextUI.text = "RPM: " + Mathf.RoundToInt(car.RPM);
        }
    }

    private void AssignLocalPlayer()
    {
        GameObject localPlayer = GameObject.FindWithTag("Player");

        if (localPlayer != null && localPlayer.GetComponent<PhotonView>().IsMine)
        {
            car = localPlayer.GetComponent<CarController>();
            carRb = localPlayer.GetComponent<Rigidbody>();

            if (car == null)
                Debug.LogWarning("A Player tagű objektum nem rendelkezik CarController-rel!");
            if (carRb == null)
                Debug.LogWarning("A Player tagű objektumnak nincs Rigidbody-je!");
        }
    }
}
