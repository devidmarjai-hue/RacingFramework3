// TrackGenerator.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MultiplayerFramework.Game
{
    public class TrackGenerator
    {
        public static Vector3[] Generate(Transform[] points, int segmentsPerCurve, out float[] cumulativeDistances, out float totalTrackLength)
        {
            List<Vector3> result = new();
            int count = points.Length;
            if (count < 2)
            {
                cumulativeDistances = new float[0];
                totalTrackLength = 0f;
                return result.ToArray();
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 p0 = points[(i - 1 + count) % count].position;
                Vector3 p1 = points[i].position;
                Vector3 p2 = points[(i + 1) % count].position;
                Vector3 p3 = points[(i + 2) % count].position;

                for (int j = 0; j < segmentsPerCurve; j++)
                {
                    float t = j / (float)segmentsPerCurve;
                    Vector3 point = 0.5f * ((2f * p1) +
                        (-p0 + p2) * t +
                        (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
                        (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
                    result.Add(point);
                }
            }

            // Zárjuk a loopot az első ponttal
            result.Add(result[0]);

            cumulativeDistances = new float[result.Count];
            cumulativeDistances[0] = 0f;
            for (int i = 1; i < result.Count; i++)
                cumulativeDistances[i] = cumulativeDistances[i - 1] + Vector3.Distance(result[i - 1], result[i]);

            totalTrackLength = cumulativeDistances.Last();
            return result.ToArray();
        }
    }
}
