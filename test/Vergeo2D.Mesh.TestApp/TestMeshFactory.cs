using System.Numerics;
using Vergeo2D.Mesh;
using Vergeo2D.Rendering;

internal static class TestMeshFactory
{
    public static Mesh2D CreateImageMesh(Texture2D texture)
    {
        var mesh = new Mesh2D();
        var topLeft = mesh.AddVertex(new Vector2(0, 0));
        var topRight = mesh.AddVertex(new Vector2(texture.Width, 0));
        var bottomRight = mesh.AddVertex(new Vector2(texture.Width, texture.Height));
        var bottomLeft = mesh.AddVertex(new Vector2(0, texture.Height));

        mesh.AddFace(topLeft, topRight, bottomRight);
        mesh.AddFace(topLeft, bottomRight, bottomLeft);
        mesh.SetTexture(texture);
        mesh.GenerateUVsFromPositions();
        return mesh;
    }

    public static float[] ExpandIndexedTriangles(MeshRenderData2D renderData)
    {
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
