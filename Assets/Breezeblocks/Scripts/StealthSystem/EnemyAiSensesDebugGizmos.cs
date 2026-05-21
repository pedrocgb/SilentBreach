using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Stealth/Enemy AI Senses Debug Gizmos")]
public class EnemyAiSensesDebugGizmos : MonoBehaviour
{
    private const float MinimumDirectionSqr = 0.0001f;
    private const int ArcSegments = 32;

    [FoldoutGroup("References")]
    [SerializeField] private ActorStatsInitializer statsInitializer;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyVisionAI enemyVisionAI;

    [FoldoutGroup("References")]
    [SerializeField] private AIHearing aiHearing;

    [FoldoutGroup("Display")]
    [SerializeField] private bool drawOnlyWhenSelected = true;

    [FoldoutGroup("Display"), MinValue(0f)]
    [SerializeField] private float labelOffset = 0.05f;

    [FoldoutGroup("Display")]
    [SerializeField] private Color visionColor = new(0.2f, 0.9f, 1f, 0.9f);

    [FoldoutGroup("Display")]
    [SerializeField] private Color fullDetectionColor = new(1f, 0.4f, 0.1f, 0.9f);

    [FoldoutGroup("Display")]
    [SerializeField] private Color hearingSilentColor = new(0.45f, 0.85f, 1f, 0.75f);

    [FoldoutGroup("Display")]
    [SerializeField] private Color hearingCommonColor = new(1f, 0.8f, 0.2f, 0.75f);

    [FoldoutGroup("Display")]
    [SerializeField] private Color hearingLoudColor = new(1f, 0.25f, 0.25f, 0.75f);

    [FoldoutGroup("Display")]
    [SerializeField] private Color labelColor = Color.white;

    public static EnemyAiSensesDebugGizmos EnsureOn(GameObject target)
    {
        if (target == null)
            return null;

        if (!target.TryGetComponent(out EnemyAiSensesDebugGizmos gizmos))
            gizmos = target.AddComponent<EnemyAiSensesDebugGizmos>();

        return gizmos;
    }

    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
        labelOffset = Mathf.Max(0f, labelOffset);
    }

    private void OnDrawGizmos()
    {
        if (drawOnlyWhenSelected)
            return;

        DrawSensesGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawOnlyWhenSelected)
            return;

        DrawSensesGizmos();
    }

    private void DrawSensesGizmos()
    {
        CacheReferences();

        DrawVisionGizmos();
        DrawHearingGizmos();
    }

    private void DrawVisionGizmos()
    {
        if (!TryResolveVisionValues(
                out Vector2 origin,
                out Vector2 forward,
                out float range,
                out float angle,
                out float fullDetectionRadius))
        {
            return;
        }

        forward = forward.sqrMagnitude > MinimumDirectionSqr ? forward.normalized : Vector2.up;
        range = Mathf.Max(0f, range);

        Gizmos.color = visionColor;
        if (angle >= 360f)
        {
            Gizmos.DrawWireSphere(origin, range);
            Gizmos.DrawLine(origin, origin + (forward * range));
        }
        else
        {
            float halfAngle = angle * 0.5f;
            Vector2 left = Rotate(forward, -halfAngle) * range;
            Vector2 right = Rotate(forward, halfAngle) * range;
            Gizmos.DrawLine(origin, origin + left);
            Gizmos.DrawLine(origin, origin + right);
            DrawArc(origin, forward, range, angle);
        }

        if (fullDetectionRadius > 0f)
        {
            Gizmos.color = fullDetectionColor;
            Gizmos.DrawWireSphere(transform.position, fullDetectionRadius);
        }

#if UNITY_EDITOR
        GUIStyle labelStyle = BuildLabelStyle();
        Handles.Label(
            (Vector3)(origin + (forward * (range + labelOffset))),
            $"Vision Range ({range:0.##})",
            labelStyle);

        Vector2 angleLabelDirection = angle >= 360f
            ? Rotate(forward, 45f)
            : Rotate(forward, angle * 0.5f);
        Handles.Label(
            (Vector3)(origin + (angleLabelDirection.normalized * (range + labelOffset))),
            $"Vision Angle ({angle:0.#})",
            labelStyle);

        if (fullDetectionRadius > 0f)
        {
            Handles.Label(
                transform.position + (Vector3.right * (fullDetectionRadius + labelOffset)),
                $"Full Detection ({fullDetectionRadius:0.##})",
                labelStyle);
        }
#endif
    }

    private void DrawHearingGizmos()
    {
        if (!TryResolveHearingValues(
                out Vector2 origin,
                out float silentRange,
                out float commonRange,
                out float loudRange))
        {
            return;
        }

        Gizmos.color = hearingSilentColor;
        Gizmos.DrawWireSphere(origin, silentRange);

        Gizmos.color = hearingCommonColor;
        Gizmos.DrawWireSphere(origin, commonRange);

        Gizmos.color = hearingLoudColor;
        Gizmos.DrawWireSphere(origin, loudRange);

#if UNITY_EDITOR
        GUIStyle labelStyle = BuildLabelStyle();
        Handles.Label(
            (Vector3)(origin + (Vector2.up * (silentRange + labelOffset))),
            $"Silent Hearing ({silentRange:0.##})",
            labelStyle);
        Handles.Label(
            (Vector3)(origin + (Vector2.left * (commonRange + labelOffset))),
            $"Common Hearing ({commonRange:0.##})",
            labelStyle);
        Handles.Label(
            (Vector3)(origin + (Vector2.right * (loudRange + labelOffset))),
            $"Loud Hearing ({loudRange:0.##})",
            labelStyle);
#endif
    }

    private bool TryResolveVisionValues(
        out Vector2 origin,
        out Vector2 forward,
        out float range,
        out float angle,
        out float fullDetectionRadius)
    {
        origin = transform.position;
        forward = transform.up;
        range = 0f;
        angle = 0f;
        fullDetectionRadius = 0f;

        EnemyVisionSettings profileVision = statsInitializer != null && statsInitializer.EnemyProfile != null
            ? statsInitializer.EnemyProfile.Vision
            : null;

        if (enemyVisionAI == null && profileVision == null)
            return false;

        if (enemyVisionAI != null)
        {
            origin = enemyVisionAI.GizmoVisionOrigin;
            forward = enemyVisionAI.GizmoForwardDirection;
            range = enemyVisionAI.ConfiguredVisionRange;
            angle = enemyVisionAI.ConfiguredVisionAngle;
            fullDetectionRadius = enemyVisionAI.ConfiguredFullDetectionRadius;
        }

        if (profileVision != null)
        {
            range = profileVision.VisionRange;
            angle = profileVision.VisionAngle;
            fullDetectionRadius = profileVision.FullDetectionRadius;
        }

        return range > 0f || fullDetectionRadius > 0f;
    }

    private bool TryResolveHearingValues(
        out Vector2 origin,
        out float silentRange,
        out float commonRange,
        out float loudRange)
    {
        origin = transform.position;
        silentRange = 0f;
        commonRange = 0f;
        loudRange = 0f;

        EnemyHearingSettings profileHearing = statsInitializer != null && statsInitializer.EnemyProfile != null
            ? statsInitializer.EnemyProfile.Hearing
            : null;

        if (aiHearing == null && profileHearing == null)
            return false;

        if (aiHearing != null)
        {
            origin = aiHearing.GizmoHearingOrigin;
            silentRange = aiHearing.ConfiguredSilentHearingRange;
            commonRange = aiHearing.ConfiguredCommonHearingRange;
            loudRange = aiHearing.ConfiguredLoudHearingRange;
        }

        if (profileHearing != null)
        {
            silentRange = profileHearing.SilentHearingRange;
            commonRange = profileHearing.CommonHearingRange;
            loudRange = profileHearing.LoudHearingRange;
        }

        return silentRange > 0f || commonRange > 0f || loudRange > 0f;
    }

    private void CacheReferences()
    {
        if (statsInitializer == null)
            statsInitializer = GetComponent<ActorStatsInitializer>();

        if (enemyVisionAI == null)
            enemyVisionAI = GetComponent<EnemyVisionAI>();

        if (aiHearing == null)
            aiHearing = GetComponent<AIHearing>();
    }

    private void DrawArc(Vector2 origin, Vector2 forward, float radius, float angle)
    {
        float halfAngle = angle * 0.5f;
        Vector2 previousPoint = origin + (Rotate(forward, -halfAngle) * radius);

        for (int i = 1; i <= ArcSegments; i++)
        {
            float t = i / (float)ArcSegments;
            float stepAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 nextPoint = origin + (Rotate(forward, stepAngle) * radius);
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            (vector.x * cos) - (vector.y * sin),
            (vector.x * sin) + (vector.y * cos));
    }

#if UNITY_EDITOR
    private GUIStyle BuildLabelStyle()
    {
        GUIStyle style = new(EditorStyles.boldLabel);
        style.normal.textColor = labelColor;
        return style;
    }
#endif
}
