using System.Numerics;

namespace Vergeo2D.Mesh;

public sealed class Mesh2D
{
    public List<Vertex2D> Vertices { get; } = new();
    public HashSet<Edge2D> Edges { get; } = new();
    public List<Face2D> Faces { get; } = new();
    public Texture2D? Texture { get; private set; }

    public void SetTexture(Texture2D? texture)
    {
        Texture = texture;
    }

    public void GenerateUVsFromPositions(bool flipY = false)
    {
        if (Texture is null) throw new InvalidOperationException("Mesh has no texture assigned.");

        foreach (var vertex in Vertices)
        {
            var uv = Texture.PixelToUV(vertex.Position);
            vertex.UV = flipY ? new Vector2(uv.X, 1f - uv.Y) : uv;
        }
    }

    public int AddVertex(Vector2 position, Vector2 uv = default)
    {
        var vertex = new Vertex2D(Vertices.Count, position) { UV = uv };
        Vertices.Add(vertex);
        return vertex.Index;
    }

    public void AddFace(int a, int b, int c)
    {
        var face = new Face2D(a, b, c);
        Faces.Add(face);
        foreach (var edge in face.GetEdges()) Edges.Add(edge);
    }

    public void RemoveVertex(int index)
    {
        Vertices.RemoveAt(index);
        for (var i = index; i < Vertices.Count; i++) Vertices[i].Index = i;

        var remainingFaces = new List<Face2D>();
        foreach (var face in Faces)
        {
            if (face.Contains(index)) continue;

            var a = face.A > index ? face.A - 1 : face.A;
            var b = face.B > index ? face.B - 1 : face.B;
            var c = face.C > index ? face.C - 1 : face.C;
            remainingFaces.Add(new Face2D(a, b, c));
        }

        Faces.Clear();
        Faces.AddRange(remainingFaces);
        RebuildEdges();
    }

    public void RebuildEdges()
    {
        Edges.Clear();
        foreach (var face in Faces)
            foreach (var edge in face.GetEdges())
                Edges.Add(edge);
    }

    public void RemoveFace(int faceIndex)
    {
        Faces.RemoveAt(faceIndex);
        RebuildEdges();
    }

    public IEnumerable<int> GetConnectedVertices(int vertexIndex)
    {
        foreach (var edge in Edges)
        {
            if (edge.A == vertexIndex) yield return edge.B;
            else if (edge.B == vertexIndex) yield return edge.A;
        }
    }

    public IEnumerable<int> GetFacesContainingVertex(int vertexIndex)
    {
        for (var i = 0; i < Faces.Count; i++)
            if (Faces[i].Contains(vertexIndex))
                yield return i;
    }

    public IEnumerable<int> GetFacesSharingEdge(Edge2D edge)
    {
        for (var i = 0; i < Faces.Count; i++)
        {
            var face = Faces[i];
            var hasA = face.Contains(edge.A);
            var hasB = face.Contains(edge.B);
            if (hasA && hasB) yield return i;
        }
    }

    public int FindFaceAt(Vector2 point)
    {
        for (var i = 0; i < Faces.Count; i++)
            if (IsPointInFace(point, Faces[i]))
                return i;

        return -1;
    }

    private bool IsPointInFace(Vector2 point, Face2D face)
    {
        var a = Vertices[face.A].Position;
        var b = Vertices[face.B].Position;
        var c = Vertices[face.C].Position;

        var d1 = Cross(point - a, b - a);
        var d2 = Cross(point - b, c - b);
        var d3 = Cross(point - c, a - c);

        var hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
        var hasPositive = d1 > 0 || d2 > 0 || d3 > 0;

        return !(hasNegative && hasPositive);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    public (Vector2 Min, Vector2 Max) GetBounds()
    {
        if (Vertices.Count == 0) return (Vector2.Zero, Vector2.Zero);

        var min = Vertices[0].Position;
        var max = Vertices[0].Position;

        foreach (var vertex in Vertices)
        {
            min = Vector2.Min(min, vertex.Position);
            max = Vector2.Max(max, vertex.Position);
        }

        return (min, max);
    }

    public Mesh2D Clone()
    {
        var clone = new Mesh2D();
        clone.SetTexture(Texture);

        foreach (var vertex in Vertices) clone.AddVertex(vertex.Position, vertex.UV);

        foreach (var face in Faces) clone.AddFace(face.A, face.B, face.C);

        return clone;
    }
}
