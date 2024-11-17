half LinearRgbToLuminance(half3 linearRgb)
{
    return dot(linearRgb, half3(0.2126729f, 0.7151522f, 0.0721750f));
}

void UISample_float(float4 originColor, float4 blurColor, float vibrancy, float brightness, float flatten,
                    float blendAmount, out float3 result)
{
    //saturate help keep color in range
    //Exclusion blend
    half3 fgScaled = lerp(half3(0, 0, 0), originColor.rgb * blendAmount, flatten);
    half3 color = saturate(blurColor + fgScaled - 2 * fgScaled * blurColor);

    //Vibrancy
    color = saturate(lerp(LinearRgbToLuminance(color), color, vibrancy));

    //Brightness
    color = saturate(color + brightness);

    result = lerp(originColor.rgb, color, blendAmount);
}
