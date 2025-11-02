using MultiplayerFramework.Game;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using System.Text;

namespace MultiplayerFramework
{
    public class GameController : MonoBehaviourPunCallbacks
    {
        [Header("Track Settings")]
        [SerializeField] private Transform[] points;
        [SerializeField] private int segmentsPerCurve = 60;
        [SerializeField] private int totalLaps = 10;

        [Header("UI")]
        [SerializeField] private TMP_Text rankingText;

        private TrackProjector projector;
        private PlayerProgressTracker progressTracker;

        private void Start()
        {
            var track = TrackGenerator.Generate(points, segmentsPerCurve, out var distances, out var length);
            projector = new TrackProjector(track, distances, length);
            progressTracker = new PlayerProgressTracker(totalLaps);
        }

        private void Update()
        {
            var players = Object.FindObjectsByType<PhotonView>(FindObjectsSortMode.None);

            foreach (var pv in players)
            {
                if (pv.CompareTag("Player"))
                {
                    var proj = projector.Project(pv.transform.position);
                    progressTracker.UpdatePlayer(pv.Owner, proj.progress);
                }
            }

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (rankingText == null)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("RANKINGS");

            int rank = 1;
            foreach (var (id, data) in progressTracker.GetRanked())
            {
                float percent = (data.totalProgress / totalLaps) * 100f;
                sb.AppendLine($"{rank++} | Player id: {id} | Lap {data.lap + 1}/{totalLaps} | {percent:F1}%");
            }

            rankingText.text = sb.ToString();
        }
    }
}
