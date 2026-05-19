using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/UI/Sound Meter UI")]
public class SoundMeterUI : MonoBehaviour
{
    [FoldoutGroup("References"), Required]
    [SerializeField] private RawImage waveformImage;

    [FoldoutGroup("Shader"), Tooltip("Float property that controls waveform intensity/noise on the material.")]
    [SerializeField] private string noiseAmountPropertyName = "_NoiseAmount";

    [FoldoutGroup("Smoothing"), MinValue(0f)]
    [SerializeField] private float riseSpeed = 10f;

    [FoldoutGroup("Smoothing"), MinValue(0f)]
    [SerializeField] private float fallSpeed = 4f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, Range(0f, 1f)]
    public float CurrentNoiseAmount => _currentNoise;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, Range(0f, 1f)]
    public float TargetNoiseAmount => _targetNoise;

    private Material _runtimeMaterial;
    private int _noiseAmountPropertyId;
    private float _currentNoise;
    private float _targetNoise;

    private void Awake()
    {
        InitializeMaterialInstance();
        RefreshShaderProperty();
    }

    private void OnValidate()
    {
        riseSpeed = Mathf.Max(0f, riseSpeed);
        fallSpeed = Mathf.Max(0f, fallSpeed);

        if (!string.IsNullOrWhiteSpace(noiseAmountPropertyName))
            _noiseAmountPropertyId = Shader.PropertyToID(noiseAmountPropertyName);
    }

    private void Update()
    {
        if (_runtimeMaterial == null)
            return;

        float speed = _targetNoise > _currentNoise ? riseSpeed : fallSpeed;
        _currentNoise = Mathf.MoveTowards(_currentNoise, _targetNoise, speed * Time.deltaTime);
        _runtimeMaterial.SetFloat(_noiseAmountPropertyId, _currentNoise);
    }

    private void OnDestroy()
    {
        if (_runtimeMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(_runtimeMaterial);
            else
                DestroyImmediate(_runtimeMaterial);
        }
    }

    public void SetTargetNoiseAmount(float targetNoise)
    {
        _targetNoise = Mathf.Clamp01(targetNoise);
    }

    public void SetNoiseAmountImmediate(float noise)
    {
        _targetNoise = Mathf.Clamp01(noise);
        _currentNoise = _targetNoise;
        RefreshShaderProperty();
    }

    private void InitializeMaterialInstance()
    {
        if (waveformImage == null || waveformImage.material == null)
            return;

        _runtimeMaterial = Instantiate(waveformImage.material);
        waveformImage.material = _runtimeMaterial;
    }

    private void RefreshShaderProperty()
    {
        if (_runtimeMaterial == null)
            return;

        _noiseAmountPropertyId = Shader.PropertyToID(noiseAmountPropertyName);
        _runtimeMaterial.SetFloat(_noiseAmountPropertyId, _currentNoise);
    }
}
