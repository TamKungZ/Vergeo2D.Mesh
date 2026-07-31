using System.Numerics;
using Vergeo2D.Mesh;
using Vergeo2D.Rendering;

internal static class MeshBackendSmokeTest
{
    public static void Run(string imagePath, MeshTestBackend backend)
    {
        var texture = Texture2D.LoadFromFile(imagePath);
        var alphaMask = ImageAlphaMask.Load(imagePath);
        var gridOptions = new MeshGridOptions2D();
        var mesh = MeshGridGenerator2D.GenerateConnectedGrid(texture, gridOptions, alphaMask);
        var overlayMesh = MeshGridGenerator2D.GenerateMaskedContourGrid(texture, alphaMask, gridOptions);
        var renderData = new MeshRenderData2D();
        var overlayRenderData = new MeshRenderData2D();

        MeshRenderExtractor.Extract(mesh, deformer: null, renderData);
        MeshRenderExtractor.Extract(overlayMesh, deformer: null, overlayRenderData);

        var drag = new RadialDragDeformer2D { Radius = Math.Max(120f, gridOptions.Spacing * 3f) };
        drag.SetDrag(new Vector2(texture.Width * 0.5f, texture.Height * 0.5f), new Vector2(16f, -12f));

        renderData.Clear();
        overlayRenderData.Clear();
        MeshRenderExtractor.Extract(mesh, drag, renderData);
        MeshRenderExtractor.Extract(overlayMesh, drag, overlayRenderData);

        if (renderData.VertexCount == 0 || renderData.IndexCount == 0)
            throw new InvalidOperationException("Backend smoke test generated empty preview render data.");

        if (overlayRenderData.VertexCount == 0 || overlayRenderData.IndexCount == 0)
            throw new InvalidOperationException("Backend smoke test generated empty overlay render data.");

        Console.WriteLine($"{GetBackendLabel(backend)} smoke test passed.");
        Console.WriteLine($"Texture: {texture.Width}x{texture.Height}");
        Console.WriteLine($"Preview mesh: {renderData.VertexCount} vertices, {renderData.IndexCount / 3} faces");
        Console.WriteLine($"Overlay mesh: {overlayRenderData.VertexCount} vertices, {overlayRenderData.IndexCount / 3} faces");
    }

    public static string GetBackendLabel(MeshTestBackend backend)
    {
        return backend switch
        {
            MeshTestBackend.OpenGL => "OpenGL",
            MeshTestBackend.Vulkan => "Vulkan",
            MeshTestBackend.DirectX => "Direct3D11",
            _ => backend.ToString()
        };
    }
}
