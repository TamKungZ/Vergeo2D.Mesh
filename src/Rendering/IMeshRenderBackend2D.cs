using Vergeo2D.Mesh;

namespace Vergeo2D.Rendering;

public interface IMeshRenderBackend2D : IDisposable
{
    RenderResourceHandle CreateResource(MeshRenderData2D data);

    void UpdateResource(RenderResourceHandle handle, MeshRenderData2D data);

    void BindTexture(RenderResourceHandle handle, Texture2D? texture);

    void Draw(RenderResourceHandle handle, in RenderTransform2D transform);

    void DestroyResource(RenderResourceHandle handle);
}

