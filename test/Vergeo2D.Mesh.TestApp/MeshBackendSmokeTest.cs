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
        var mesh = MeshGridGenerator2D.GenerateMaskedContourGrid(texture, alphaMask, gridOptions);
        var overlayMesh = mesh.Clone();
        var renderData = new MeshRenderData2D();
        var overlayRenderData = new MeshRenderData2D();

        MeshRenderExtractor.Extract(mesh, deformer: null, renderData);
        MeshRenderExtractor.Extract(overlayMesh, deformer: null, overlayRenderData);
        if (!SameMeshSurface(renderData, overlayRenderData))
            throw new InvalidOperationException("Preview texture mesh and UV overlay mesh must start from the same 2D surface.");

        if (HasTJunctions(renderData))
            throw new InvalidOperationException("Preview texture mesh contains T-junctions that can open cracks during deformation.");

        var originalOverlayVertices = UvOverlayRenderer.BuildFaceVertices(
            overlayRenderData.Vertices,
            overlayRenderData.Indices,
            imageOrigin: Vector2.Zero,
            imageScale: 1f);
        var originalMeshUvs = mesh.Vertices.Select(vertex => vertex.UV).ToArray();
        var originalOverlayUvs = overlayMesh.Vertices.Select(vertex => vertex.UV).ToArray();

        var drag = new RadialDragDeformer2D { Radius = Math.Max(120f, gridOptions.Spacing * 3f) };
        drag.SetDrag(new Vector2(texture.Width * 0.5f, texture.Height * 0.5f), new Vector2(16f, -12f));

        renderData.Clear();
        overlayRenderData.Clear();
        MeshRenderExtractor.Extract(mesh, drag, renderData);
        MeshRenderExtractor.Extract(overlayMesh, drag, overlayRenderData);
        if (!SameMeshSurface(renderData, overlayRenderData))
            throw new InvalidOperationException("Preview texture mesh and UV overlay mesh diverged during drag preview.");

        var deformedOverlayVertices = UvOverlayRenderer.BuildFaceVertices(
            overlayRenderData.Vertices,
            overlayRenderData.Indices,
            imageOrigin: Vector2.Zero,
            imageScale: 1f);

        if (renderData.VertexCount == 0 || renderData.IndexCount == 0)
            throw new InvalidOperationException("Backend smoke test generated empty preview render data.");

        if (overlayRenderData.VertexCount == 0 || overlayRenderData.IndexCount == 0)
            throw new InvalidOperationException("Backend smoke test generated empty overlay render data.");

        if (originalOverlayVertices.AsSpan().SequenceEqual(deformedOverlayVertices))
            throw new InvalidOperationException("Overlay geometry did not move with the deformed texture preview.");

        if (!UvsMatch(renderData, originalMeshUvs) || !UvsMatch(overlayRenderData, originalOverlayUvs))
            throw new InvalidOperationException("Drag preview changed mesh UV values.");

        mesh.ApplyDeformer(drag);
        overlayMesh.ApplyDeformer(drag);

        if (!originalMeshUvs.AsSpan().SequenceEqual(mesh.Vertices.Select(vertex => vertex.UV).ToArray()) ||
            !originalOverlayUvs.AsSpan().SequenceEqual(overlayMesh.Vertices.Select(vertex => vertex.UV).ToArray()))
        {
            throw new InvalidOperationException("Committed drag changed mesh UV values.");
        }

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

    private static bool UvsMatch(MeshRenderData2D renderData, Vector2[] expectedUvs)
    {
        if (renderData.VertexCount != expectedUvs.Length) return false;

        var vertices = renderData.Vertices;
        for (var i = 0; i < expectedUvs.Length; i++)
        {
            var offset = i * MeshRenderData2D.FloatsPerVertex;
            if (new Vector2(vertices[offset + 2], vertices[offset + 3]) != expectedUvs[i])
                return false;
        }

        return true;
    }

    private static bool SameMeshSurface(MeshRenderData2D preview, MeshRenderData2D overlay)
    {
        if (preview.VertexCount != overlay.VertexCount || preview.IndexCount != overlay.IndexCount)
            return false;

        if (!preview.Indices.SequenceEqual(overlay.Indices))
            return false;

        var previewVertices = preview.Vertices;
        var overlayVertices = overlay.Vertices;
        if (previewVertices.Length != overlayVertices.Length)
            return false;

        for (var i = 0; i < previewVertices.Length; i++)
            if (previewVertices[i] != overlayVertices[i])
                return false;

        return true;
    }

    private static bool HasTJunctions(MeshRenderData2D renderData)
    {
        var vertices = renderData.Vertices;
        var positions = new Vector2[renderData.VertexCount];
        for (var i = 0; i < positions.Length; i++)
        {
            var offset = i * MeshRenderData2D.FloatsPerVertex;
            positions[i] = new Vector2(vertices[offset], vertices[offset + 1]);
        }

        foreach (var edge in GetUniqueEdges(renderData.Indices))
        {
            var a = positions[edge.A];
            var b = positions[edge.B];
            for (var vertexIndex = 0; vertexIndex < positions.Length; vertexIndex++)
            {
                if (vertexIndex == edge.A || vertexIndex == edge.B) continue;
                if (IsPointOnSegment(positions[vertexIndex], a, b))
                    return true;
            }
        }

        return false;
    }

    private static IEnumerable<(int A, int B)> GetUniqueEdges(ReadOnlySpan<int> indices)
    {
        var edges = new HashSet<(int A, int B)>();
        for (var i = 0; i < indices.Length; i += 3)
        {
            AddEdge(edges, indices[i], indices[i + 1]);
            AddEdge(edges, indices[i + 1], indices[i + 2]);
            AddEdge(edges, indices[i + 2], indices[i]);
        }

        return edges;
    }

    private static void AddEdge(HashSet<(int A, int B)> edges, int a, int b)
    {
        edges.Add(a < b ? (a, b) : (b, a));
    }

    private static bool IsPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        const float tolerance = 0.001f;
        var edge = b - a;
        var pointOffset = point - a;
        var lengthSquared = edge.LengthSquared();
        if (lengthSquared <= tolerance) return false;

        var cross = MathF.Abs(edge.X * pointOffset.Y - edge.Y * pointOffset.X);
        if (cross > tolerance * MathF.Sqrt(lengthSquared)) return false;

        var dot = Vector2.Dot(pointOffset, edge);
        return dot > tolerance && dot < lengthSquared - tolerance;
    }
}
