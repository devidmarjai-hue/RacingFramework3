using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PG_Physics.Wheel;

/// <summary>
/// Wheel settings and update logic (FXController removed).
/// </summary>
[System.Serializable]
public struct Wheel
{
    public WheelCollider WheelCollider;
    public Transform WheelView;
    public float SlipForGenerateParticle;
    public Vector3 TrailOffset;

    public float CurrentMaxSlip => Mathf.Max(CurrentForwardSlip, CurrentSidewaysSlip);
    public float CurrentForwardSlip { get; private set; }
    public float CurrentSidewaysSlip { get; private set; }
    public WheelHit GetHit => Hit;
    public bool StopEmitFX { get; set; }

    private WheelHit Hit;
    private PG_WheelCollider m_PGWC;

    public PG_WheelCollider PG_WheelCollider
    {
        get
        {
            if (WheelCollider == null)
                return null;

            if (m_PGWC == null)
            {
                m_PGWC = WheelCollider.GetComponent<PG_WheelCollider>();
                if (m_PGWC == null)
                {
                    m_PGWC = WheelCollider.gameObject.AddComponent<PG_WheelCollider>();
                    m_PGWC.CheckFirstEnable();
                }
            }
            return m_PGWC;
        }
    }

    /// <summary>
    /// Update gameplay logic (slip values).
    /// </summary>
    public void FixedUpdate()
    {
        if (WheelCollider == null)
            return;

        if (WheelCollider.GetGroundHit(out Hit))
        {
            float prevForward = CurrentForwardSlip;
            float prevSide = CurrentSidewaysSlip;

            CurrentForwardSlip = (prevForward + Mathf.Abs(Hit.forwardSlip)) * 0.5f;
            CurrentSidewaysSlip = (prevSide + Mathf.Abs(Hit.sidewaysSlip)) * 0.5f;
        }
        else
        {
            CurrentForwardSlip = 0f;
            CurrentSidewaysSlip = 0f;
        }
    }

    /// <summary>
    /// Update visual (wheel mesh only).
    /// </summary>
    public void UpdateVisual(bool carIsVisible)
    {
        if (!carIsVisible || WheelCollider == null || WheelView == null)
            return;

        UpdateTransform();
    }

    /// <summary>
    /// Update wheel visual transform from WheelCollider.
    /// </summary>
    public void UpdateTransform()
    {
        if (WheelCollider == null || WheelView == null)
            return;

        WheelCollider.GetWorldPose(out Vector3 pos, out Quaternion quat);
        WheelView.position = pos;
        WheelView.rotation = quat;
    }

    /// <summary>
    /// Update friction configuration on the PG_WheelCollider.
    /// </summary>
    public void UpdateFrictionConfig(PG_WheelColliderConfig config)
    {
        if (PG_WheelCollider != null)
            PG_WheelCollider.UpdateConfig(config);
    }
}
