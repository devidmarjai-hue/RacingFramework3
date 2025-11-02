using Photon.Pun;
using UnityEngine;

namespace MultiplayerFramework
{
    public class LapResetTrigger : MonoBehaviour
    {
        [SerializeField] private LapTimer lapTimer;

        private void OnTriggerEnter(Collider other)
        {
            // Keresünk PhotonView komponenst a belépő objektumon
            if (!other.TryGetComponent(out PhotonView pv))
                return;

            // Csak akkor fut, ha ez a local player
            if (!pv.IsMine)
                return;

            // Csak "Player" tag esetén, és ha van LapTimer
            if (other.CompareTag("Player") && lapTimer != null)
            {
                //Debug.Log("Local player triggered lap reset");
                lapTimer.ResetTimer();
            }
        }
    }
}
