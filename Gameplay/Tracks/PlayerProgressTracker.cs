using System.Collections.Generic;
using System.Linq;
using Photon.Realtime;

namespace MultiplayerFramework.Game
{
    public class PlayerProgressTracker
    {
        private readonly int totalLaps;
        private readonly Dictionary<int, PlayerProgress> playerProgresses = new();

        public PlayerProgressTracker(int totalLaps)
        {
            this.totalLaps = totalLaps;
        }

        public class PlayerProgress
        {
            public int lap;
            public float trackProgress;
            public float lastTrackProgress;
            public float totalProgress;
            public void Update() => totalProgress = lap + trackProgress;
        }

        public void UpdatePlayer(Player player, float progress)
        {
            if (!playerProgresses.TryGetValue(player.ActorNumber, out var data))
            {
                data = new PlayerProgress { trackProgress = progress };
                playerProgresses[player.ActorNumber] = data;
            }

            data.lastTrackProgress = data.trackProgress;
            data.trackProgress = progress;

            if (data.lastTrackProgress > 0.9f && data.trackProgress < 0.1f && data.lap < totalLaps)
                data.lap++;

            data.Update();
        }

        public IEnumerable<(int id, PlayerProgress data)> GetRanked()
        {
            return playerProgresses
                .OrderByDescending(p => p.Value.totalProgress)
                .Select(p => (p.Key, p.Value));
        }

        public PlayerProgress GetPlayerProgress(int actorId)
        {
            playerProgresses.TryGetValue(actorId, out var p);
            return p;
        }
    }
}
