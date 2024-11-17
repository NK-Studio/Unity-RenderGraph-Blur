using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule.Util;

//This example blits the active CameraColor to a new texture. It shows how to do a blit with material, and how to use the ResourceData to avoid another blit back to the active color target.
//This example is for API demonstrative purposes. 


// This pass blits the whole screen for a given material to a temp texture, and swaps the UniversalResourceData.cameraColor to this temp texture.
// Therefor, the next pass that references the cameraColor will reference this new temp texture as the cameraColor, saving us a blit. 
// Using the ResourceData, you can manage swapping of resources yourself and don't need a bespoke API like the SwapColorBuffer API that was specific for the cameraColor. 
// This allows you to write more decoupled passes without the added costs of avoidable copies/blits.
public class BlitAndSwapColorPass : ScriptableRenderPass
{
    const string m_PassName = "BlitAndSwapColorPass";

    // Material used in the blit operation.
    Material m_BlitMaterial;

    // Function used to transfer the material from the renderer feature to the render pass.
    public void Setup(Material mat)
    {
        m_BlitMaterial = mat;

        // 패스는 현재 색상 텍스처를 읽습니다. 중간 텍스처여야 합니다. BackBuffer를 입력 텍스처로 사용하는 것은 지원되지 않습니다. 
        // 이 속성을 설정하면 URP가 자동으로 중간 텍스처를 생성합니다. 성능 비용이 발생하므로 필요하지 않은 경우에는 설정하지 마세요.
        // RenderFeature에서 설정하는 것이 아니라 여기에서 설정하는 것이 좋습니다. 이렇게 하면 패스가 자체 포함되며 이를 사용하여 RenderFeature 없이 단일 동작에서 패스를 직접 대기열에 추가할 수 있습니다.
        requiresIntermediateTexture = true;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        // UniversalResourceData에는 활성 색상 및 깊이 텍스처를 포함하여 렌더러에서 사용하는 모든 텍스처 핸들이 포함됩니다.
        // 활성 색상 및 깊이 텍스처는 카메라가 렌더링하는 기본 색상 및 깊이 버퍼입니다.
        var resourceData = frameData.Get<UniversalResourceData>();

        // m_Pass.requiresIntermediateTexture = true로 설정했기 때문에 이런 일이 발생해서는 안 됩니다.
        // 렌더링 이벤트를 AfterRendering으로 설정하지 않는 한 BackBuffer만 있습니다.
        if (resourceData.isActiveTargetBackBuffer)
        {
            Debug.LogError(
                $"렌더 패스를 건너뛰는 중입니다. BlitAndSwapColorRendererFeature에는 중간 ColorTexture가 필요하므로 BackBuffer를 텍스처 입력으로 사용할 수 없습니다.");
            return;
        }

        // 대상 텍스처가 여기에 생성됩니다. 
        // 텍스처는 활성 색상 텍스처와 동일한 크기로 생성됩니다.
        var source = resourceData.activeColorTexture;

        var destinationDesc = renderGraph.GetTextureDesc(source);
        destinationDesc.name = $"CameraColor-{m_PassName}";
        destinationDesc.clearBuffer = false;

        TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

        RenderGraphUtils.BlitMaterialParameters para = new(source, destination, m_BlitMaterial, 0);
        renderGraph.AddBlitPass(para, passName: m_PassName);

        // FrameData를 사용하면 내부 파이프라인 버퍼를 가져오고 설정할 수 있습니다. 여기서는 CameraColorBuffer를 이 패스에서 방금 쓴 텍스처로 업데이트합니다. 
        // RenderGraph는 파이프라인 리소스와 종속성을 관리하기 때문에 후속 패스는 올바른 색상 버퍼를 올바르게 사용합니다.
        // 이 최적화에는 몇 가지 주의 사항이 있습니다. 카메라 스태킹과 같이 프레임 전체와 서로 다른 카메라 간에 색상 버퍼가 지속되는 경우 주의해야 합니다.
        // 이 경우 텍스처가 RTHandle인지 확인하고 텍스처의 수명 주기를 적절하게 관리해야 합니다.
        resourceData.cameraColor = destination;
    }
}

public class BlitAndSwapColorRendererFeature : ScriptableRendererFeature
{
    [Tooltip("The material used when making the blit operation.")]
    public Material material;

    [Tooltip("The event where to inject the pass.")]
    public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    BlitAndSwapColorPass m_Pass;

    // Here you can create passes and do the initialization of them. This is called everytime serialization happens.
    public override void Create()
    {
        m_Pass = new BlitAndSwapColorPass();

        // Configures where the render pass should be injected.
        m_Pass.renderPassEvent = renderPassEvent;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Early exit if there are no materials.
        if (material == null)
        {
            Debug.LogWarning(this.name + " material is null and will be skipped.");
            return;
        }

        m_Pass.Setup(material);
        renderer.EnqueuePass(m_Pass);
    }
}