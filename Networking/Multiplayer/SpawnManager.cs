using Photon.Pun;
using UnityEngine;

namespace MultiplayerFramework
{
    public class SpawnManager : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform[] spawnPoints;

        [Header("Camera Settings")]
        [SerializeField] private CameraFollow cameraFollow;

        private void Start()
        {
            SpawnPlayer();
        }

        /// <summary>
        /// Spawnolja a játékost sorrendben a spawn pontokra (nem random).
        /// </summary>
        public void SpawnPlayer()
        {
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                Debug.LogWarning("⚠️ Photon not connected yet!");
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("❌ Player prefab not assigned!");
                return;
            }

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError("❌ No spawn points assigned!");
                return;
            }

            // ActorNumber alapján választunk spawn pontot
            int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            int index = (actorNumber - 1) % spawnPoints.Length;
            Transform spawn = spawnPoints[index];

            // Létrehozzuk a játékost
            GameObject player = PhotonNetwork.Instantiate(playerPrefab.name, spawn.position, spawn.rotation);

            // Kamera követése
            if (cameraFollow == null)
            {
                cameraFollow = Camera.main.GetComponent<CameraFollow>();
            }

            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(player.transform);
            }

            Debug.Log($"✅ Player {actorNumber} spawned at index {index} ({spawn.position})");
        }
    }
}
