using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("UI/Blur UI")]
public class UIBlur : UIBehaviour, IMaterialModifier
{
    [Tooltip("UI에 적용되는 흐림 정도를 조정합니다.")]
    [SerializeField, Range(0, 1)] private float blendAmount = 1f;
    [Tooltip("흐릿한 UI의 생동감을 조정합니다.")]
    [SerializeField, Range(-1, 3)] private float vibrancy = 1f;
    [Tooltip("UI의 밝기를 조정합니다.")]
    [SerializeField, Range(-1, 1)] private float brightness = 0.1f;
    [Tooltip("UI의 병합 정도를 조정합니다.")]
    [SerializeField, Range(0, 1)] private float flatten;

    private Material _modifiedMaterial;

    private Image _image;
    private Image TargetImage => _image ? _image : _image = GetComponent<Image>();

    // Constants
    private readonly int BlendAmountPropertyId = Shader.PropertyToID("_BlendAmount");
    private readonly int VibrancyPropertyId = Shader.PropertyToID("_Vibrancy");
    private readonly int BrightnessPropertyId = Shader.PropertyToID("_Brightness");
    private readonly int FlattenPropertyId = Shader.PropertyToID("_Flatten");

    #region Properties

    public float BlendAmount
    {
        get => blendAmount;
        set
        {
            blendAmount = value;
            if (TargetImage != null)
                TargetImage.SetMaterialDirty();
        }
    }

    public float Vibrancy
    {
        get => vibrancy;
        set
        {
            vibrancy = value;
            if (TargetImage != null)
                TargetImage.SetMaterialDirty();
        }
    }

    public float Brightness
    {
        get => brightness;
        set
        {
            brightness = value;
            if (TargetImage != null)
                TargetImage.SetMaterialDirty();
        }
    }

    public float Flatten
    {
        get => flatten;
        set
        {
            flatten = value;
            if (TargetImage != null)
                TargetImage.SetMaterialDirty();
        }
    }

    #endregion

    protected override void OnEnable()
    {
        base.OnEnable();

        if (TargetImage != null)
            TargetImage.SetMaterialDirty();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (_modifiedMaterial != null)
            DestroyImmediate(_modifiedMaterial);

        _modifiedMaterial = null;

        if (TargetImage != null)
            TargetImage.SetMaterialDirty();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        if (!IsActive() || TargetImage == null) return;
        TargetImage.SetMaterialDirty();
    }
#endif

    protected override void OnDidApplyAnimationProperties()
    {
        base.OnDidApplyAnimationProperties();
        if (!IsActive() || TargetImage == null) return;
        TargetImage.SetMaterialDirty();
    }

    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (!IsActive() || _image == null ||
            !baseMaterial.HasProperty(BlendAmountPropertyId) ||
            !baseMaterial.HasProperty(VibrancyPropertyId) ||
            !baseMaterial.HasProperty(BrightnessPropertyId) ||
            !baseMaterial.HasProperty(FlattenPropertyId))
        {
            return baseMaterial;
        }

        if (_modifiedMaterial == null)
        {
            _modifiedMaterial = new Material(baseMaterial);
            _modifiedMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        _modifiedMaterial.CopyPropertiesFromMaterial(baseMaterial);

        _modifiedMaterial.SetFloat(BlendAmountPropertyId, blendAmount);
        _modifiedMaterial.SetFloat(VibrancyPropertyId, vibrancy);
        _modifiedMaterial.SetFloat(BrightnessPropertyId, brightness);
        _modifiedMaterial.SetFloat(FlattenPropertyId, flatten);

        return _modifiedMaterial;
    }
}