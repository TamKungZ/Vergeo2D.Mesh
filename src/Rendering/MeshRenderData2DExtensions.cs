namespace Vergeo2D.Rendering;

public static class MeshRenderData2DExtensions
{
    public static float[] ExpandIndexedTriangles(this MeshRenderData2D renderData)
    {
        if (renderData is null) throw new ArgumentNullException(nameof(renderData));

        var sourceVertices = renderData.Vertices;
        var sourceIndices = renderData.Indices;
        var expanded = new float[sourceIndices.Length * MeshRenderData2D.FloatsPerVertex];

        for (var i = 0; i < sourceIndices.Length; i++)
        {
            var sourceOffset = sourceIndices[i] * MeshRenderData2D.FloatsPerVertex;
            var targetOffset = i * MeshRenderData2D.FloatsPerVertex;

            expanded[targetOffset] = sourceVertices[sourceOffset];
            expanded[targetOffset + 1] = sourceVertices[sourceOffset + 1];
            expanded[targetOffset + 2] = sourceVertices[sourceOffset + 2];
            expanded[targetOffset + 3] = sourceVertices[sourceOffset + 3];
        }

        return expanded;
    }
}

