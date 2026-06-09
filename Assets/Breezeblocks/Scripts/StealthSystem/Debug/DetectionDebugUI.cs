using TMPro;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/UI/Detection Debug UI")]
public class DetectionDebugUI : MonoBehaviour
{
    private const string DefaultGeneratedTextName = "Detection Debug Text";
    private const float MinimumRefreshInterval = 0.02f;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyVisionAI observedEnemy;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerVisibility playerVisibility;

    [FoldoutGroup("References")]
    [SerializeField] private AIHearing observedHearing;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMovementController observedMovementController;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyCombatantAI observedCombatant;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text tmpText;

    [FoldoutGroup("References")]
    [SerializeField] private Text legacyText;

    [FoldoutGroup("Behavior")]
    [Tooltip("If enabled, missing references are resolved once on enable.")]
    [SerializeField] private bool autoFindSceneReferences = true;

    [FoldoutGroup("Behavior")]
    [SerializeField, MinValue(MinimumRefreshInterval), SuffixLabel("s", true)] private float refreshInterval = 0.1f;

    [FoldoutGroup("Behavior")]
    [SerializeField] private bool includeSensorDetails = true;

    [FoldoutGroup("Generated TMP Text"), ShowIf(nameof(ShouldShowGeneratedTextSettings))]
    [SerializeField] private int generatedFontSize = 24;

    [FoldoutGroup("Generated TMP Text"), ShowIf(nameof(ShouldShowGeneratedTextSettings))]
    [SerializeField] private Color generatedTextColor = Color.white;

    [FoldoutGroup("Generated TMP Text"), ShowIf(nameof(ShouldShowGeneratedTextSettings))]
    [SerializeField] private Vector2 generatedAnchorMin = new(0f, 1f);

    [FoldoutGroup("Generated TMP Text"), ShowIf(nameof(ShouldShowGeneratedTextSettings))]
    [SerializeField] private Vector2 generatedAnchorMax = new(0f, 1f);

    [FoldoutGroup("Generated TMP Text"), ShowIf(nameof(ShouldShowGeneratedTextSettings))]
    [SerializeField] private Vector2 generatedPivot = new(0f, 1f);

    [FoldoutGroup("Generated TMP Text"), ShowIf(nameof(ShouldShowGeneratedTextSettings))]
    [SerializeField] private Vector2 generatedAnchoredPosition = new(16f, -16f);

    [FoldoutGroup("Generated TMP Text"), ShowIf(nameof(ShouldShowGeneratedTextSettings))]
    [SerializeField] private Vector2 generatedSizeDelta = new(420f, 180f);

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, MultiLineProperty(6)]
    public string CurrentDisplayText => currentDisplayText;

    [SerializeField, HideInInspector] private string currentDisplayText;

    private float _nextRefreshTime;

    private void Awake()
    {
        ClampSettings();
        ResolveReferences();
        EnsureTextReference();
        RefreshDisplay(force: true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureTextReference();
        _nextRefreshTime = Time.time;
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    private void Update()
    {
        if (Time.time < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.time + refreshInterval;
        RefreshDisplay(force: false);
    }

    private void ResolveReferences()
    {
        if (observedEnemy == null && autoFindSceneReferences)
            observedEnemy = FindFirstObjectByType<EnemyVisionAI>();

        if (playerVisibility == null)
        {
            if (observedEnemy != null && observedEnemy.TargetVisibilityComponent != null)
                playerVisibility = observedEnemy.TargetVisibilityComponent;
            else if (autoFindSceneReferences)
                playerVisibility = FindFirstObjectByType<PlayerVisibility>();
        }

        if (observedHearing == null && observedEnemy != null)
            observedHearing = observedEnemy.GetComponent<AIHearing>();

        if (observedHearing == null && autoFindSceneReferences)
            observedHearing = FindFirstObjectByType<AIHearing>();

        if (observedMovementController == null && observedEnemy != null)
            observedMovementController = observedEnemy.GetComponent<EnemyMovementController>();

        if (observedMovementController == null && observedHearing != null)
            observedMovementController = observedHearing.GetComponent<EnemyMovementController>();

        if (observedMovementController == null && autoFindSceneReferences)
            observedMovementController = FindFirstObjectByType<EnemyMovementController>();

        if (observedCombatant == null && observedMovementController != null)
            observedCombatant = observedMovementController.GetComponent<EnemyCombatantAI>();

        if (observedCombatant == null && observedEnemy != null)
            observedCombatant = observedEnemy.GetComponent<EnemyCombatantAI>();

        if (observedCombatant == null && autoFindSceneReferences)
            observedCombatant = FindFirstObjectByType<EnemyCombatantAI>();

        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();

        if (legacyText == null)
            legacyText = GetComponent<Text>();

        if (tmpText == null)
        {
            TMP_Text[] childTmpTexts = GetComponentsInChildren<TMP_Text>(true);
            if (childTmpTexts.Length > 0)
                tmpText = childTmpTexts[0];
        }

        if (legacyText == null)
        {
            Text[] childTexts = GetComponentsInChildren<Text>(true);
            if (childTexts.Length > 0)
                legacyText = childTexts[0];
        }
    }

    private void EnsureTextReference()
    {
        if (tmpText != null || legacyText != null)
            return;

        GameObject textObject = new GameObject(DefaultGeneratedTextName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = generatedAnchorMin;
        rectTransform.anchorMax = generatedAnchorMax;
        rectTransform.pivot = generatedPivot;
        rectTransform.anchoredPosition = generatedAnchoredPosition;
        rectTransform.sizeDelta = generatedSizeDelta;

        TextMeshProUGUI generatedText = textObject.GetComponent<TextMeshProUGUI>();
        generatedText.font = TMP_Settings.defaultFontAsset;
        generatedText.fontSize = generatedFontSize;
        generatedText.color = generatedTextColor;
        generatedText.alignment = TextAlignmentOptions.TopLeft;
        generatedText.textWrappingMode = TextWrappingModes.NoWrap;
        generatedText.overflowMode = TextOverflowModes.Overflow;
        generatedText.raycastTarget = false;
        tmpText = generatedText;
    }

    private void RefreshDisplay(bool force)
    {
        string nextDisplayText = BuildDisplayText();
        if (!force && currentDisplayText == nextDisplayText)
            return;

        currentDisplayText = nextDisplayText;

        if (tmpText != null)
            tmpText.text = nextDisplayText;

        if (legacyText != null)
            legacyText.text = nextDisplayText;
    }

    private string BuildDisplayText()
    {
        string enemyState = observedMovementController != null
            ? observedMovementController.CurrentState.ToString()
            : observedEnemy != null ? observedEnemy.CurrentState.ToString() : "--";
        float visibility = playerVisibility != null ? playerVisibility.CurrentVisibility : 0f;
        float detection = observedEnemy != null ? observedEnemy.CurrentDetectionValue : 0f;

        string displayText =
            $"Enemy State: {enemyState}\n" +
            $"Player Visibility: {visibility:0.00}\n" +
            $"Detection Value: {detection:0.00}";

        if (observedMovementController != null)
        {
            displayText +=
                $"\nMove Speed: {observedMovementController.CurrentMovementSpeed:0.00}" +
                $"\nSpeed Cap: {observedMovementController.CurrentSpeedCap:0.00}" +
                $"\nCurrent Destination: {observedMovementController.CurrentTargetPosition}" +
                $"\nIs Moving: {observedMovementController.IsMoving}" +
                $"\nReached Destination: {observedMovementController.HasReachedDestination}";

            if (observedMovementController.UsingItinerary)
            {
                displayText +=
                    $"\nItinerary Step: {observedMovementController.CurrentItineraryIndex} - {observedMovementController.CurrentItineraryStepName}" +
                    $"\nItinerary Time Left: {observedMovementController.CurrentItineraryStepRemainingTime:0.00}" +
                    $"\nItinerary Paused: {observedMovementController.IsItineraryPaused}";
            }
        }

        if (observedCombatant != null)
        {
            displayText +=
                $"\nDrafted: {observedCombatant.IsDrafted}" +
                $"\nCombat Mode: {observedCombatant.CurrentCombatMode}" +
                $"\nEnemy Ammo: {observedCombatant.CurrentLoadedAmmo} / {observedCombatant.CurrentReserveAmmo}" +
                $"\nEnemy Accurate: {observedCombatant.IsAccurate}" +
                $"\nCombat Delay Left: {observedCombatant.CombatDelayRemaining:0.00}" +
                $"\nLost Sight Linger: {observedCombatant.LostSightLingerRemaining:0.00}" +
                $"\nLost Sight Shoot Linger: {observedCombatant.LostSightShootingRemaining:0.00}";
        }

        if (!includeSensorDetails || observedEnemy == null)
            return displayText;

        displayText +=
            $"\nCan Detect Player: {observedEnemy.CanCurrentlyDetectTarget}" +
            $"\nInside Range: {observedEnemy.TargetInRange}" +
            $"\nInside Cone: {observedEnemy.TargetInsideVisionCone}" +
            $"\nLine Of Sight: {observedEnemy.HasLineOfSight}" +
            $"\nCan See Flashlight Signal: {observedEnemy.CanCurrentlySeeFlashlight}" +
            $"\nTracking Flashlight Source: {observedEnemy.HasActiveFlashlightStimulus}" +
            $"\nFlashlight Source Position: {observedEnemy.LastKnownFlashlightSourcePosition}" +
            $"\nFlashlight Memory Left: {observedEnemy.FlashlightStimulusTimeRemaining:0.00}s" +
            $"\nVisibility Threshold: {observedEnemy.VisibilityThreshold:0.00}" +
            $"\nTarget Visibility Sample: {observedEnemy.CurrentTargetVisibility:0.00}" +
            $"\nTarget Distance: {observedEnemy.CurrentTargetDistance:0.00}" +
            $"\nDistance Multiplier: {observedEnemy.CurrentDistanceDetectionMultiplier:0.00}x";

        if (observedHearing != null)
        {
            displayText +=
                $"\nLast Heard Type: {observedHearing.LastHeardNoiseType}" +
                $"\nLast Heard Value: {observedHearing.LastHeardNoiseValue:0.00}" +
                $"\nLast Heard Position: {observedHearing.LastHeardNoisePosition}" +
                $"\nHearing Ignored By Vision: {observedHearing.HearingIgnoredBecauseOfVisualDetection}";
        }

        return displayText;
    }

    private void ClampSettings()
    {
        refreshInterval = Mathf.Max(MinimumRefreshInterval, refreshInterval);
        generatedFontSize = Mathf.Max(1, generatedFontSize);
        generatedSizeDelta.x = Mathf.Max(0f, generatedSizeDelta.x);
        generatedSizeDelta.y = Mathf.Max(0f, generatedSizeDelta.y);
    }

    private bool ShouldShowGeneratedTextSettings()
    {
        return tmpText == null && legacyText == null;
    }
}
