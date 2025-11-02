using UnityEngine;

namespace MultiplayerFramework.Game
{
    public struct TrackProjection
    {
        public Vector3 closestPoint;
        public float progress;
    }

    public class TrackProjector
    {
        private readonly Vector3[] trackPositions;
        private readonly float[] cumulativeDistances;
        private readonly float totalTrackLength;

        public TrackProjector(Vector3[] trackPositions, float[] cumulativeDistances, float totalTrackLength)
        {
            this.trackPositions = trackPositions;
            this.cumulativeDistances = cumulativeDistances;
            this.totalTrackLength = totalTrackLength;
        }

        public TrackProjection Project(Vector3 position)
        {
            TrackProjection projection = new();
            if (trackPositions == null || trackPositions.Length < 2)
                return projection;

            float minDistSqr = float.MaxValue;
            int closestIndex = 0;
            float tOnSegment = 0f;

            for (int i = 0; i < trackPositions.Length - 1; i++)
            {
                Vector3 a = trackPositions[i];
                Vector3 b = trackPositions[i + 1];
                Vector3 ab = b - a;
                float abSqr = ab.sqrMagnitude;
                if (abSqr <= Mathf.Epsilon) continue;

                float t = Mathf.Clamp01(Vector3.Dot(position - a, ab) / abSqr);
                Vector3 proj = a + t * ab;
                float distSqr = (position - proj).sqrMagnitude;

                if (distSqr < minDistSqr)
                {
                    minDistSqr = distSqr;
                    closestIndex = i;
                    tOnSegment = t;
                    projection.closestPoint = proj;
                }
            }

            float worldDist = cumulativeDistances[closestIndex] +
                tOnSegment * Vector3.Distance(trackPositions[closestIndex], trackPositions[closestIndex + 1]);

            projection.progress = Mathf.Clamp01(worldDist / totalTrackLength);
            return projection;
        }

        public Vector3 GetPointAlongProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            float distance = progress * totalTrackLength;

            int index = 0;
            while (index < cumulativeDistances.Length - 1 && cumulativeDistances[index + 1] < distance)
                index++;

            float t = Mathf.InverseLerp(cumulativeDistances[index], cumulativeDistances[index + 1], distance);
            return Vector3.Lerp(trackPositions[index], trackPositions[index + 1], t);
        }

        // <-- Hozzáadva a TrackLength getter
        public int TrackLength => trackPositions.Length;
    }
}
