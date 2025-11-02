using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MultiplayerFramework
{
    public class LobbyManager : MonoBehaviourPunCallbacks
    {
        [Header("UI References")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private RoomListUI roomListUI;
        [SerializeField] private TMP_Text gameStatusText;

        [Header("Start Grid")]
        [SerializeField] private Transform startGridParent; // <-- IDE HÚZD BE A StartGrid-et!

        private void Awake()
        {
            PhotonNetwork.AutomaticallySyncScene = true;

            if (hostButton != null) hostButton.interactable = false;
            if (gameStatusText != null) gameStatusText.gameObject.SetActive(false);

            if (string.IsNullOrEmpty(PhotonNetwork.NickName))
                PhotonNetwork.NickName = "Player" + Random.Range(1000, 9999);

            if (hostButton != null) hostButton.onClick.AddListener(HostRoom);
            if (disconnectButton != null) disconnectButton.onClick.AddListener(Disconnect);
            if (startGameButton != null) startGameButton.onClick.AddListener(StartGame);

            if (!PhotonNetwork.IsConnected)
                PhotonNetwork.ConnectUsingSettings();
            else if (!PhotonNetwork.InLobby)
                PhotonNetwork.JoinLobby();
        }

        #region Photon Callbacks
        public override void OnConnectedToMaster()
        {
            if (hostButton != null) hostButton.interactable = true;
            if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby();
        }

        public override void OnJoinedLobby()
        {
            Debug.Log("Joined Lobby");
        }

        public override void OnCreatedRoom()
        {
            if (hostButton != null) hostButton.interactable = false;

            if (startGameButton != null)
            {
                startGameButton.interactable = true;
                startGameButton.gameObject.SetActive(true);
            }

            if (gameStatusText != null)
            {
                gameStatusText.text = "Waiting for players...";
                gameStatusText.gameObject.SetActive(true);
            }
        }

        public override void OnJoinedRoom()
        {
            if (gameStatusText != null)
            {
                gameStatusText.text = PhotonNetwork.IsMasterClient ?
                    "You are the host. Start when ready." :
                    "Waiting for host...";
                gameStatusText.gameObject.SetActive(true);
            }

            if (startGameButton != null)
            {
                startGameButton.interactable = PhotonNetwork.IsMasterClient;
                startGameButton.gameObject.SetActive(true);
            }

            var spawnManager = FindFirstObjectByType<SpawnManager>();
            if (spawnManager != null)
                spawnManager.SpawnPlayer();
        }

        public override void OnLeftRoom()
        {
            if (roomListUI != null) roomListUI.ClearRoomList();
            if (gameStatusText != null) gameStatusText.gameObject.SetActive(false);
            if (startGameButton != null) startGameButton.gameObject.SetActive(false);
            if (hostButton != null) hostButton.interactable = true;
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (hostButton != null) hostButton.interactable = false;
            if (gameStatusText != null) gameStatusText.gameObject.SetActive(false);
            if (startGameButton != null) startGameButton.gameObject.SetActive(false);
            if (roomListUI != null) roomListUI.ClearRoomList();
        }
        #endregion

        #region Room Actions
        public void HostRoom()
        {
            if (!PhotonNetwork.IsConnectedAndReady) return;

            string roomName = "Room" + Random.Range(1000, 9999);
            PhotonNetwork.CreateRoom(roomName, new RoomOptions { MaxPlayers = 12 });
        }

        public void JoinRoomInList(string roomName)
        {
            PhotonNetwork.JoinRoom(roomName);
        }

        public void Disconnect()
        {
            if (PhotonNetwork.InRoom)
                PhotonNetwork.LeaveRoom();
            else if (PhotonNetwork.IsConnected)
                PhotonNetwork.Disconnect();
        }

        public void StartGame()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            AssignStartPositions();
        }
        #endregion

        #region Start Position Assignment
        private void AssignStartPositions()
        {
            var players = PhotonNetwork.PlayerList;

            for (int i = 0; i < players.Length; i++)
                photonView.RPC(nameof(RPC_SetPlayerToStartPos), players[i], i);
        }

        [PunRPC]
        void RPC_SetPlayerToStartPos(int index)
        {
            if (startGridParent == null)
            {
                Debug.LogError("StartGridParent nincs beállítva!");
                return;
            }

            if (index >= startGridParent.childCount)
            {
                Debug.LogError("Nincs elég rajthely!");
                return;
            }

            Transform startPos = startGridParent.GetChild(index);

            // Local Player GameObject
            var player = PhotonNetwork.LocalPlayer.TagObject as GameObject;
            if (player == null)
            {
                foreach (var view in FindObjectsByType<PhotonView>(FindObjectsSortMode.None))
                    if (view.IsMine)
                        player = view.gameObject;

                PhotonNetwork.LocalPlayer.TagObject = player;
            }

            if (player == null) return;

            // Reset rigidbody motion before teleport
            if (player.TryGetComponent(out Rigidbody rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            player.transform.SetPositionAndRotation(startPos.position, startPos.rotation);
        }
        #endregion
    }
}
