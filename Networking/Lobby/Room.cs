using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using TMPro;

namespace MultiplayerFramework
{
    public class Room : MonoBehaviour
    {
        public TMP_Text Name;

        public void JoinRoom()
        {
            // Megkeresi a LobbyManager komponenst a jelenetben
            LobbyManager roomManager = GameObject.Find("LobbyManager")?.GetComponent<LobbyManager>();

            if (roomManager != null)
            {
                roomManager.JoinRoomInList(Name.text);
            }
            else
            {
                Debug.LogError("❌ LobbyManager object or component not found in the scene!");
            }
        }
    }
}
