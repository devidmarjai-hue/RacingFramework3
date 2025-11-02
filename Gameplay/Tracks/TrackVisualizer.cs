// TrackVisualizer.cs
using UnityEngine;
using MultiplayerFramework.Game;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class TrackVisualizer : MonoBehaviour
{
    [Header("Track Settings")]
    public Transform[] trackPoints;
    public int segmentsPerCurve = 10;

    private LineRenderer lineRenderer;
    private Vector3[] trackPositions;
    private float[] cumulativeDistances;
    private float totalTrackLength;

    void Awake()
    {
        Init();
        GenerateTrack();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        Init();
        if (trackPoints != null && trackPoints.Length > 1)
            GenerateTrack();
    }
#endif

    void Init()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true; // loopoljon a LineRenderer
            lineRenderer.widthMultiplier = 0.2f;
        }

        if (lineRenderer.sharedMaterial == null)
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = Color.yellow;
            lineRenderer.sharedMaterial = mat;
        }
    }

    void GenerateTrack()
    {
        if (trackPoints == null || trackPoints.Length < 2)
            return;

        trackPositions = TrackGenerator.Generate(trackPoints, segmentsPerCurve, out cumulativeDistances, out totalTrackLength);

        lineRenderer.positionCount = trackPositions.Length;
        lineRenderer.SetPositions(trackPositions);
    }

    public Vector3 GetClosestPointOnTrack(Vector3 position)
    {
        TrackProjector projector = new(trackPositions, cumulativeDistances, totalTrackLength);
        return projector.Project(position).closestPoint;
    }

    void OnDrawGizmos()
    {
        if (trackPoints == null || trackPoints.Length < 2)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Init();
            GenerateTrack();
        }
#endif

        Gizmos.color = Color.green;
        for (int i = 0; i < trackPoints.Length; i++)
        {
            if (trackPoints[i] && trackPoints[(i + 1) % trackPoints.Length])
                Gizmos.DrawLine(trackPoints[i].position, trackPoints[(i + 1) % trackPoints.Length].position);
        }
    }
}
