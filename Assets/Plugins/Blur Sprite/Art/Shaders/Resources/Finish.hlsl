half LinearRgbToLuminance(half3 linearRgb)
{
    return dot(linearRgb, half3(0.2126729f, 0.7151522f, 0.0721750f));
}

void UISample_float(float4 originColor, float4 blurColor, float vibrancy, float brightness, float flatten,
                    float blendAmount, out float3 result)
{
    float3 fgScaled = lerp(float3(0, 0, 0), originColor.rgb * blendAmount.xxx, flatten.xxx);
    float3 color = saturate(blurColor.rgb + fgScaled - float3(2, 2, 2) * fgScaled * blurColor.rgb);

    // Vibrancy
    color = saturate(lerp(LinearRgbToLuminance(color), color, vibrancy.xxx));

    // Brightness
    color = saturate(color + brightness.xxx);

    result = lerp(originColor.rgb, color, blendAmount.xxx);
}

void UISample_half(half4 originColor, half4 blurColor, half vibrancy, half brightness, half flatten,
                   half blendAmount, out half3 result)
{
    half3 fgScaled = lerp(half3(0, 0, 0), originColor.rgb * blendAmount.xxx, flatten.xxx);
    half3 color = saturate(blurColor.rgb + fgScaled - half3(2, 2, 2) * fgScaled * blurColor.rgb);

    // Vibrancy
    color = saturate(lerp(LinearRgbToLuminance(color), color, vibrancy.xxx));

    // Brightness
    color = saturate(color + brightness.xxx);

    result = lerp(originColor.rgb, color, blendAmount.xxx);
}
