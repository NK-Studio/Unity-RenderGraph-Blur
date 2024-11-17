using System;
using UnityEngine;

public class Slider : MonoBehaviour
{
    private static readonly int BlendAmount = Shader.PropertyToID("_BlendAmount");
    private SpriteRenderer _spriteRenderer;

    private void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }
    
    public void SetIntensity(float value)
    {
        _spriteRenderer.material.SetFloat(BlendAmount, value);
    }
}
