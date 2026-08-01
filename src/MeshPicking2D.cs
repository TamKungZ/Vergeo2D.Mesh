using System.Numerics;

namespace Vergeo2D.Mesh;

public static class MeshPicking2D
{
    public static bool FindNearestVertex(
        Mesh2D mesh,
        Vector2 point,
        float maxDistance,
        out int vertexIndex,
        out float distanceSquared)
    {
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));

        vertexIndex = -1;
        distanceSquared = maxDistance * maxDistance;

        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            var candidateDistance = Vector2.DistanceSquared(point, mesh.Vertices[i].Position);
            if (candidateDistance > distanceSquared) continue;

            vertexIndex = i;
            distanceSquared = candidateDistance;
        }

        return vertexIndex >= 0;
    }

    public static bool FindNearestEdge(
        Mesh2D mesh,
        Vector2 point,
        float maxDistance,
        out Edge2D edge,
        out Vector2 closestPoint,
        out float distanceSquared)
    {
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));

        edge = default;
        closestPoint = Vector2.Zero;
        distanceSquared = maxDistance * maxDistance;
        var found = false;

        foreach (var candidate in mesh.Edges)
        {
            if ((uint)candidate.A >= (uint)mesh.Vertices.Count || (uint)candidate.B >= (uint)mesh.Vertices.Count)
                continue;

            var a = mesh.Vertices[candidate.A].Position;
            var b = mesh.Vertices[candidate.B].Position;
            var candidatePoint = ClosestPointOnSegment(point, a, b);
            var candidateDistance = Vector2.DistanceSquared(point, candidatePoint);
            if (candidateDistance > distanceSquared) continue;

            edge = candidate;
            closestPoint = candidatePoint;
            distanceSquared = candidateDistance;
            found = true;
        }

        return found;
    }

    public static bool TryGetFaceBarycentric(Mesh2D mesh, int faceIndex, Vector2 point, out Vector3 barycentric)
    {
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));
        if ((uint)faceIndex >= (uint)mesh.Faces.Count)
        {
            barycentric = Vector3.Zero;
            return false;
        }

        var face = mesh.Faces[faceIndex];
        if ((uint)face.A >= (uint)mesh.Vertices.Count ||
            (uint)face.B >= (uint)mesh.Vertices.Count ||
            (uint)face.C >= (uint)mesh.Vertices.Count)
        {
            barycentric = Vector3.Zero;
            return false;
        }

        var a = mesh.Vertices[face.A].Position;
        var b = mesh.Vertices[face.B].Position;
        var c = mesh.Vertices[face.C].Position;
        var denominator = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
        if (MathF.Abs(denominator) < 0.000001f)
        {
            barycentric = Vector3.Zero;
            return false;
        }

        var u = ((b.Y - c.Y) * (point.X - c.X) + (c.X - b.X) * (point.Y - c.Y)) / denominator;
        var v = ((c.Y - a.Y) * (point.X - c.X) + (a.X - c.X) * (point.Y - c.Y)) / denominator;
        var w = 1f - u - v;
        barycentric = new Vector3(u, v, w);
        return true;
    }

    public static bool TryGetFaceUV(Mesh2D mesh, int faceIndex, Vector2 point, out Vector2 uv)
    {
        if (!TryGetFaceBarycentric(mesh, faceIndex, point, out var barycentric))
        {
            uv = Vector2.Zero;
            return false;
        }

        var face = mesh.Faces[faceIndex];
        uv =
            mesh.Vertices[face.A].UV * barycentric.X +
            mesh.Vertices[face.B].UV * barycentric.Y +
            mesh.Vertices[face.C].UV * barycentric.Z;
        return true;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var segment = b - a;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0f) return a;

        var amount = Vector2.Dot(point - a, segment) / lengthSquared;
        amount = Clamp(amount, 0f, 1f);
        return a + segment * amount;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
