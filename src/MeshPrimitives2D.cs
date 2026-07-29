using System.Numerics;

namespace Vergeo2D.Mesh;

public static class MeshPrimitives2D
{
    public static Mesh2D CreateTexturedQuad(Texture2D texture, bool flipY = false)
    {
        if (texture is null) throw new ArgumentNullException(nameof(texture));
        return CreateTexturedQuad(texture, Vector2.Zero, new Vector2(texture.Width, texture.Height), flipY);
    }

    public static Mesh2D CreateTexturedQuad(Texture2D texture, Vector2 origin, Vector2 size, bool flipY = false)
    {
        if (texture is null) throw new ArgumentNullException(nameof(texture));

        var mesh = new Mesh2D();
        var topLeft = mesh.AddVertex(origin);
        var topRight = mesh.AddVertex(origin + new Vector2(size.X, 0f));
        var bottomRight = mesh.AddVertex(origin + size);
        var bottomLeft = mesh.AddVertex(origin + new Vector2(0f, size.Y));

        mesh.AddFace(topLeft, topRight, bottomRight);
        mesh.AddFace(topLeft, bottomRight, bottomLeft);
        mesh.SetTexture(texture);
        mesh.GenerateUVsFromPositions(flipY);
        return mesh;
    }
}

