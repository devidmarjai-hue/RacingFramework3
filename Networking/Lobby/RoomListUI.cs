using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MultiplayerFramework
{
    public class RoomListUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform content;           // ScrollView Content
        [SerializeField] private GameObject roomButtonPrefab; // Gomb prefab

        private readonly List<GameObject> roomButtons = new();
        public string SelectedRoom { get; private set; } = null;

        /// <summary>
        /// Frissíti a lobbyban megjelenő szobák listáját.
        /// </summary>
        public void UpdateRoomList(List<RoomInfo> roomList)
        {
            ClearRoomList();

            foreach (var room in roomList)
            {
                // Ne jelenítsük meg a törölt vagy érvénytelen szobákat
                if (room.RemovedFromList || string.IsNullOrEmpty(room.Name))
                    continue;

                GameObject buttonObj = Instantiate(roomButtonPrefab, content);

                // Szöveg beállítása
                TMP_Text text = buttonObj.GetComponentInChildren<TMP_Text>();
                if (text != null)
                    text.text = $"{room.Name} ({room.PlayerCount}/{room.MaxPlayers})";

                // Gomb esemény beállítása
                Button button = buttonObj.GetComponent<Button>();
                if (button != null)
                {
                    string roomName = room.Name;
                    button.onClick.AddListener(() =>
                    {
                        SelectedRoom = roomName;
                        Debug.Log($"🟢 Selected room: {SelectedRoom}");
                    });
                }
                else
                {
                    Debug.LogWarning($"⚠️ Room prefab '{roomButtonPrefab.name}' has no Button component!");
                }

                roomButtons.Add(buttonObj);
            }
        }

        /// <summary>
        /// Törli a korábbi szobagombokat a listából.
        /// </summary>
        public void ClearRoomList()
        {
            foreach (var btn in roomButtons)
            {
                if (btn != null)
                    Destroy(btn);
            }

            roomButtons.Clear();
            SelectedRoom = null;
        }
    }
}
