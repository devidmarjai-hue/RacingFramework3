using UnityEngine;
using Photon.Pun;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace MultiplayerFramework.Game
{
    public class GridManager : MonoBehaviourPun
    {
        [Header("Grid Positions Parent")]
        public Transform gridParent;

        [Header("Player Tag")]
        public string playerTag = "Player";

        [Header("Debug / Wait settings")]
        public float waitTimeoutSeconds = 5f; // mennyi ideig várjon a kliensen, hogy spawnoljanak a player GameObjectek
        public float pollInterval = 0.1f;

        public void TeleportPlayers(PlayerProgressTracker progressTracker)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                Debug.Log("[GridManager] Not master client — TeleportPlayers aborted.");
                return;
            }

            var rankedPlayers = progressTracker.GetRanked()
                .Select(p => p.id)
                .ToArray();

            Debug.Log($"[GridManager] Master sending teleport RPC with {rankedPlayers.Length} player ids: {string.Join(",", rankedPlayers)}");
            photonView.RPC(nameof(RPC_TeleportPlayers), RpcTarget.All, rankedPlayers);
        }

        [PunRPC]
        private void RPC_TeleportPlayers(int[] rankedPlayerIds)
        {
            Debug.Log($"[GridManager] RPC_TeleportPlayers called on client. Received {rankedPlayerIds?.Length ?? 0} ids: {(rankedPlayerIds != null ? string.Join(",", rankedPlayerIds) : "null")}");
            StartCoroutine(RunTeleportWhenReady(rankedPlayerIds));
        }

        private IEnumerator RunTeleportWhenReady(int[] rankedPlayerIds)
        {
            if (gridParent == null)
            {
                Debug.LogError("[GridManager] gridParent is NOT set! Assign it in inspector.");
                yield break;
            }

            // Grid pozíciók gyűjtése
            var gridPositions = gridParent
                .GetComponentsInChildren<Transform>()
                .Where(t => t != gridParent)
                .OrderBy(t => t.name)
                .ToArray();

            Debug.Log($"[GridManager] Found {gridPositions.Length} grid positions.");

            // Várunk, hogy a player GameObject-ek (tag-el) megjelenjenek a jelenetben, de maximum waitTimeoutSeconds-ig
            float timer = 0f;
            List<(GameObject go, PhotonView view)> foundPlayers = new List<(GameObject, PhotonView)>();

            while (timer < waitTimeoutSeconds)
            {
                foundPlayers = GameObject.FindGameObjectsWithTag(playerTag)
                    .Select(go => (go, go.GetComponent<PhotonView>()))
                    .Where(x => x.Item2 != null)
                    .Select(x => (x.go, x.Item2))
                    .ToList();

                // Debug: felsoroljuk az eddig talált PhotonView owner-okat
                var owners = foundPlayers.Select(x => x.view.Owner != null ? x.view.Owner.ActorNumber.ToString() : "null").ToArray();
                Debug.Log($"[GridManager] Polling players (t={timer:F2}s): found {foundPlayers.Count} tagged objects. Owners: {string.Join(",", owners)}");

                // Ha találtunk legalább valamelyik actorId-hez tartozó objektumot, kiléphetünk hamarabb
                bool allFoundAtLeastOne = rankedPlayerIds.All(id => foundPlayers.Any(fp => fp.view.Owner != null && fp.view.Owner.ActorNumber == id));
                if (allFoundAtLeastOne || foundPlayers.Count >= rankedPlayerIds.Length)
                    break;

                timer += pollInterval;
                yield return new WaitForSeconds(pollInterval);
            }

            // Ha nincs elég grid pozíció
            if (rankedPlayerIds == null || rankedPlayerIds.Length == 0)
            {
                Debug.LogWarning("[GridManager] No ranked player ids provided.");
                yield break;
            }

            if (gridPositions.Length == 0)
            {
                Debug.LogWarning("[GridManager] No grid positions found under gridParent.");
                yield break;
            }

            // Friss lista (utolsó állapot)
            foundPlayers = GameObject.FindGameObjectsWithTag(playerTag)
                .Select(go => (go, go.GetComponent<PhotonView>()))
                .Where(x => x.Item2 != null)
                .Select(x => (x.go, x.Item2))
                .ToList();

            Debug.Log($"[GridManager] Final found players: {foundPlayers.Count}. Will try to match ranked ids.");

            // Teleportálás: sorrend a rankedPlayerIds szerint, de csak ha találtunk PhotonView.Owner.ActorNumber egyezést
            int assigned = 0;
            for (int i = 0; i < rankedPlayerIds.Length && i < gridPositions.Length; i++)
            {
                int actorId = rankedPlayerIds[i];

                var match = foundPlayers.FirstOrDefault(x => x.view.Owner != null && x.view.Owner.ActorNumber == actorId);

                if (match.go == null)
                {
                    // Fallback 1: nézzük meg a TagObject-hez rendelést, hátha be van állítva
                    var fallbackPlayer = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == actorId);
                    GameObject fallbackGo = null;
                    if (fallbackPlayer != null && fallbackPlayer.TagObject is GameObject toGo)
                    {
                        fallbackGo = toGo;
                        Debug.Log($"[GridManager] Fallback: Using TagObject for actor {actorId}.");
                    }

                    if (fallbackGo == null)
                    {
                        Debug.LogWarning($"[GridManager] Could not find GameObject for actor {actorId} (rank index {i}). Skipping.");
                        continue;
                    }
                    else
                    {
                        match = (fallbackGo, fallbackGo.GetComponent<PhotonView>());
                        if (match.view == null)
                            Debug.LogWarning($"[GridManager] Fallback GameObject for actor {actorId} has no PhotonView.");
                    }
                }

                // Ha megvan a match.go, teleportáljuk
                if (match.go != null)
                {
                    match.go.transform.SetPositionAndRotation(gridPositions[i].position, gridPositions[i].rotation);
                    Debug.Log($"[GridManager] Teleported actor {actorId} -> grid {i} ({gridPositions[i].name}).");
                    assigned++;
                }
            }

            Debug.Log($"[GridManager] Teleport finished. Assigned: {assigned}/{Mathf.Min(rankedPlayerIds.Length, gridPositions.Length)}.");
        }
    }
}
