using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//여러 ScriptableRenderPass를 사용하여 프레임 데이터를 사용하여 Blit 작업을 처리할 수 있는 방법의 예입니다.
public class BlitRendererFeature : ScriptableRendererFeature
{
    // frameData에 존재하는 클래스입니다. 텍스처 리소스 관리를 담당합니다.
    public class BlitData : ContextItem, IDisposable
    {
        // 블릿 작업에 사용되는 텍스처입니다.
        RTHandle m_TextureFront;

        RTHandle m_TextureBack;

        // Render graph texture handles.
        TextureHandle m_TextureHandleFront;
        TextureHandle m_TextureHandleBack;

        // 스케일 바이어스는 블릿 작업이 수행되는 방식을 제어하는 데 사용됩니다. x 및 y 매개변수는 배율을 제어합니다.
        // 그리고 z와 w는 오프셋을 제어합니다.
        static Vector4 scaleBias = new Vector4(1f, 1f, 0f, 0f);

        // 어떤 텍스처가 가장 많이 반응하는지 관리하는 부울입니다.
        bool m_IsFront = true;

        // 가장 최근에 블릿 작업을 수행한 색상 버퍼를 포함하는 텍스처입니다.
        public TextureHandle texture;

        // BlitData를 초기화하는 데 사용되는 함수입니다. 각 프레임에 대해 클래스 사용을 시작하기 전에 호출해야 합니다.
        public void Init(RenderGraph renderGraph, RenderTextureDescriptor targetDescriptor, string textureName = null)
        {
            // 텍스처 이름이 유효한지 확인하고 그렇지 않으면 기본값을 입력합니다.
            var texName = String.IsNullOrEmpty(textureName) ? "_BlitTextureData" : textureName;
            // RTHandles가 처음으로 초기화되거나 마지막 프레임 이후 targetDescriptor가 변경된 경우 재할당합니다.
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_TextureFront, targetDescriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: texName + "Front");
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_TextureBack, targetDescriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: texName + "Back");
            // 렌더 그래프에서 RTHandles를 가져와 렌더 그래프 내에 텍스처 핸들을 만듭니다.
            m_TextureHandleFront = renderGraph.ImportTexture(m_TextureFront);
            m_TextureHandleBack = renderGraph.ImportTexture(m_TextureBack);
            // 활성 텍스처를 프런트 버퍼로 설정합니다.
            texture = m_TextureHandleFront;
        }

        // 유효하지 않은 텍스처 핸들 누출을 방지하려면 각 프레임 후에 텍스처 핸들을 재설정해야 합니다.
        // 텍스처 핸들은 한 프레임 동안만 유지되기 때문입니다.
        public override void Reset()
        {
            // 다음 프레임에 잘못된 참조가 전달되는 것을 방지하기 위해 색상 버퍼를 재설정합니다.
            // 이는 이제 유효하지 않은 마지막 프레임의 BlitData 텍스처 핸들일 수 있습니다.
            m_TextureHandleFront = TextureHandle.nullHandle;
            m_TextureHandleBack = TextureHandle.nullHandle;
            texture = TextureHandle.nullHandle;
            // 활성 텍스처를 프런트 버퍼로 재설정합니다.
            m_IsFront = true;
        }

        // 데이터를 렌더링 기능으로 전송하는 데 사용하는 데이터입니다.
        class PassData
        {
            // 블릿 작업을 수행할 때 소스, 대상 및 자료가 필요합니다.
            // 원본과 대상은 복사할 위치를 아는 데 사용됩니다.
            public TextureHandle source;

            public TextureHandle destination;

            // 재료는 복사하는 동안 색상 버퍼를 변환하는 데 사용됩니다.
            public Material material;
        }

        // 이 함수의 경우 값을 재설정하는 것을 기억해야 함을 보여주기 위해 머티리얼을 인수로 사용하지 않습니다.
        // 마지막 프레임에서 값이 누출되는 것을 방지하기 위해 사용하지 않습니다.
        public void RecordBlitColor(RenderGraph renderGraph, ContextContainer frameData)
        {
            // BlitData를 초기화하지 않은 경우 BlitData의 텍스처가 유효한지 확인하세요.
            if (!texture.IsValid())
            {
                // BlitData에 사용하는 설명자를 설정합니다. 카메라 대상의 설명자를 시작으로 사용해야 합니다.
                var cameraData = frameData.Get<UniversalCameraData>();
                var descriptor = cameraData.cameraTargetDescriptor;
                // blit 작업에 대해 MSAA를 비활성화합니다.
                descriptor.msaaSamples = 1;
                // 색상 버퍼로만 변환하므로 깊이 버퍼를 비활성화합니다.
                descriptor.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
                Init(renderGraph, descriptor);
            }

            // 패스 이름이 지정된 렌더 그래프 패스 기록을 시작합니다.
            // 및 렌더링 기능의 실행에 데이터를 전달하는 데 사용되는 데이터를 출력하는 단계를 포함합니다.
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("BlitColorPass", out var passData))
            {
                // 카메라의 활성 색상 첨부 파일을 검색하려면 프레임 데이터에서 UniversalResourceData를 가져옵니다.
                var resourceData = frameData.Get<UniversalResourceData>();

                // 마지막 프레임의 값이 포함되어 있으므로 재질을 재설정하는 것을 잊지 마세요.
                // 이렇게 하지 않으면 객체 할당을 재사용하기 때문에
                // 렌더 그래프를 사용하여 Blit Pass Data에 대한 마지막 커밋 자료를 얻게 됩니다.
                passData.material = null;
                passData.source = resourceData.activeColorTexture;
                passData.destination = texture;

                // 카메라 색상 버퍼에 대한 입력 첨부를 설정합니다.
                builder.UseTexture(passData.source);
                // 출력 첨부 0을 BlitData의 활성 텍스처로 설정합니다.
                builder.SetRenderAttachment(passData.destination, 0);

                // Sets the render function.
                builder.SetRenderFunc((PassData passData, RasterGraphContext rgContext) =>
                    ExecutePass(passData, rgContext));
            }
        }

        // Records a render graph render pass which blits the BlitData's active texture back to the camera's color attachment.
        public void RecordBlitBackToColor(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Check if BlitData's texture is valid if it isn't it hasn't been initialized or an error has occured.
            if (!texture.IsValid()) return;

            // Starts the recording of the render graph pass given the name of the pass
            // and outputting the data used to pass data to the execution of the render function.
            using (var builder = renderGraph.AddRasterRenderPass<PassData>($"BlitBackToColorPass", out var passData))
            {
                // Fetch UniversalResourceData from frameData to retrive the camera's active color attachment.
                var resourceData = frameData.Get<UniversalResourceData>();

                // Remember to reset material. Otherwise you would use the last material used in RecordFullScreenPass.
                passData.material = null;
                passData.source = texture;
                passData.destination = resourceData.activeColorTexture;

                // Sets input attachment to BitData's active texture.
                builder.UseTexture(passData.source);
                // Sets output attachment 0 to the cameras color buffer.
                builder.SetRenderAttachment(passData.destination, 0);

                // Sets the render function.
                builder.SetRenderFunc((PassData passData, RasterGraphContext rgContext) =>
                    ExecutePass(passData, rgContext));
            }
        }

        // This function blits the whole screen for a given material.
        public void RecordFullScreenPass(RenderGraph renderGraph, string passName, Material material)
        {
            // Checks if the data is previously initialized and if the material is valid.
            if (!texture.IsValid() || material == null)
            {
                Debug.LogWarning("Invalid input texture handle, will skip fullscreen pass.");
                return;
            }

            // Starts the recording of the render graph pass given the name of the pass
            // and outputting the data used to pass data to the execution of the render function.
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
            {
                // Switching the active texture handles to avoid blit. If we want the most recent
                // texture we can simply look at the variable texture
                m_IsFront = !m_IsFront;

                // Setting data to be used when executing the render function.
                passData.material = material;
                passData.source = texture;

                // Swap the active texture.
                if (m_IsFront)
                    passData.destination = m_TextureHandleFront;
                else
                    passData.destination = m_TextureHandleBack;

                // Sets input attachment to BlitData's old active texture.
                builder.UseTexture(passData.source);
                // Sets output attachment 0 to BitData's new active texture.
                builder.SetRenderAttachment(passData.destination, 0);

                // Update the texture after switching.
                texture = passData.destination;

                // Sets the render function.
                builder.SetRenderFunc((PassData passData, RasterGraphContext rgContext) =>
                    ExecutePass(passData, rgContext));
            }
        }

        // ExecutePass is the render function for each of the blit render graph recordings.
        // This is good practice to avoid using variables outside of the lambda it is called from.
        // It is static to avoid using member variables which could cause unintended behaviour.
        static void ExecutePass(PassData data, RasterGraphContext rgContext)
        {
            // We can use blit with or without a material both using the static scaleBias to avoid reallocations.
            if (data.material == null)
                Blitter.BlitTexture(rgContext.cmd, data.source, scaleBias, 0, false);
            else
                Blitter.BlitTexture(rgContext.cmd, data.source, scaleBias, data.material, 0);
        }

        // We need to release the textures once the renderer is released which will dispose every item inside
        // frameData (also data types previously created in earlier frames).
        public void Dispose()
        {
            m_TextureFront?.Release();
            m_TextureBack?.Release();
        }
    }

    // Initial render pass for the renderer feature which is run to initialize the data in frameData and copying
    // the camera's color attachment to a texture inside BlitData so we can do transformations using blit.
    class BlitStartRenderPass : ScriptableRenderPass
    {
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Creating the data BlitData inside frameData.
            var blitTextureData = frameData.Create<BlitData>();
            // Copies the camera's color attachment to a texture inside BlitData.
            blitTextureData.RecordBlitColor(renderGraph, frameData);
        }
    }

    // Render pass which makes a blit for each material given to the renderer feature.
    class BlitRenderPass : ScriptableRenderPass
    {
        List<Material> m_Materials;

        // Setup function used to retrive the materials from the renderer feature.
        public void Setup(List<Material> materials)
        {
            m_Materials = materials;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Retrives the BlitData from the current frame.
            var blitTextureData = frameData.Get<BlitData>();
            foreach (var material in m_Materials)
            {
                // Skip current cycle if the material is null since there is no need to blit if no
                // transformation happens.
                if (material == null) continue;
                // Records the material blit pass.
                blitTextureData.RecordFullScreenPass(renderGraph, $"Blit {material.name} Pass", material);
            }
        }
    }

    // Final render pass to copying the texture back to the camera's color attachment.
    class BlitEndRenderPass : ScriptableRenderPass
    {
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Retrives the BlitData from the current frame and blit it back again to the camera's color attachment.
            var blitTextureData = frameData.Get<BlitData>();
            blitTextureData.RecordBlitBackToColor(renderGraph, frameData);
        }
    }

    [SerializeField]
    [Tooltip(
        "Materials used for blitting. They will be blit in the same order they have in the list starting from index 0. ")]
    List<Material> m_Materials;

    BlitStartRenderPass m_StartPass;
    BlitRenderPass m_BlitPass;
    BlitEndRenderPass m_EndPass;

    // Here you can create passes and do the initialization of them. This is called everytime serialization happens.
    public override void Create()
    {
        m_StartPass = new BlitStartRenderPass();
        m_BlitPass = new BlitRenderPass();
        m_EndPass = new BlitEndRenderPass();

        // Configures where the render pass should be injected.
        m_StartPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        m_BlitPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        m_EndPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Early return if there is no texture to blit.
        if (m_Materials == null || m_Materials.Count == 0) return;

        // Pass the material to the blit render pass.
        m_BlitPass.Setup(m_Materials);

        // Since they have the same RenderPassEvent the order matters when enqueueing them.
        renderer.EnqueuePass(m_StartPass);
        renderer.EnqueuePass(m_BlitPass);
        renderer.EnqueuePass(m_EndPass);
    }
}