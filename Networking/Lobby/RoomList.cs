using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace MultiplayerFramework
{
    public class RoomList : MonoBehaviourPunCallbacks
    {
        [Header("Room List Settings")]
        [SerializeField] private GameObject roomPrefab;
        [SerializeField] private Transform contentParent;

        // A frissítések közti tisztítás érdekében eltároljuk a létrehozott elemeket
        private readonly List<GameObject> spawnedRooms = new();

        public override void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            // Ha nincs beállítva a contentParent, próbálja automatikusan megtalálni
            if (contentParent == null)
            {
                GameObject contentObj = GameObject.Find("Content");
                if (contentObj != null)
                    contentParent = contentObj.transform;
                else
                {
                    Debug.LogError("❌ 'Content' object not found in the scene!");
                    return;
                }
            }

            // Előző lista törlése
            ClearRoomList();

            // Szobák kirajzolása
            foreach (RoomInfo info in roomList)
            {
                if (info.RemovedFromList || string.IsNullOrEmpty(info.Name))
                    continue;

                Debug.Log($"🟢 Found Room: {info.Name}");

                GameObject room = Instantiate(roomPrefab, contentParent);
                room.GetComponent<Room>().Name.text = info.Name;

                spawnedRooms.Add(room);
            }
        }

        public void ClearRoomList()
        {
            foreach (var room in spawnedRooms)
            {
                if (room != null)
                    Destroy(room);
            }

            spawnedRooms.Clear();
        }
    }
}
