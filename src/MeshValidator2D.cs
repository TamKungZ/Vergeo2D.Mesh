using System.Numerics;

namespace Vergeo2D.Mesh;

public static class MeshValidator2D
{
    public static MeshValidationResult2D Validate(Mesh2D mesh, MeshValidationOptions2D? options = null)
    {
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));

        options ??= new MeshValidationOptions2D();

        var issues = new List<MeshValidationIssue2D>();
        var usedVertices = new bool[mesh.Vertices.Count];
        var duplicateFaces = options.ReportDuplicateFaces
            ? new Dictionary<FaceKey, int>()
            : null;
        var edgeUses = options.ReportNonManifoldEdges || options.ReportInconsistentWinding
            ? new Dictionary<Edge2D, List<EdgeUse>>()
            : null;

        for (var faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
        {
            var face = mesh.Faces[faceIndex];
            if (!ValidateFaceIndices(mesh, face, faceIndex, issues)) continue;

            usedVertices[face.A] = true;
            usedVertices[face.B] = true;
            usedVertices[face.C] = true;

            if (face.A == face.B || face.B == face.C || face.C == face.A)
            {
                issues.Add(new MeshValidationIssue2D(
                    MeshValidationSeverity2D.Error,
                    "DegenerateFace",
                    "Face references the same vertex more than once.",
                    faceIndex: faceIndex));
                continue;
            }

            var area = TriangleArea(
                mesh.Vertices[face.A].Position,
                mesh.Vertices[face.B].Position,
                mesh.Vertices[face.C].Position);
            if (area < Math.Max(0f, options.MinimumTriangleArea))
            {
                issues.Add(new MeshValidationIssue2D(
                    MeshValidationSeverity2D.Warning,
                    "SmallTriangle",
                    $"Face triangle area is below {options.MinimumTriangleArea}.",
                    faceIndex: faceIndex));
            }

            if (duplicateFaces is not null)
            {
                var key = new FaceKey(face.A, face.B, face.C);
                if (duplicateFaces.TryGetValue(key, out var firstFaceIndex))
                {
                    issues.Add(new MeshValidationIssue2D(
                        MeshValidationSeverity2D.Warning,
                        "DuplicateFace",
                        $"Face duplicates the vertex set from face {firstFaceIndex}.",
                        faceIndex: faceIndex));
                }
                else
                {
                    duplicateFaces[key] = faceIndex;
                }
            }

            if (edgeUses is not null)
            {
                AddEdgeUse(edgeUses, face.A, face.B, faceIndex);
                AddEdgeUse(edgeUses, face.B, face.C, faceIndex);
                AddEdgeUse(edgeUses, face.C, face.A, faceIndex);
            }
        }

        if (options.ReportOrphanVertices)
        {
            for (var vertexIndex = 0; vertexIndex < usedVertices.Length; vertexIndex++)
            {
                if (!usedVertices[vertexIndex])
                {
                    issues.Add(new MeshValidationIssue2D(
                        MeshValidationSeverity2D.Warning,
                        "OrphanVertex",
                        "Vertex is not referenced by any face.",
                        vertexIndex: vertexIndex));
                }
            }
        }

        if (edgeUses is not null)
        {
            foreach (var pair in edgeUses)
            {
                var uses = pair.Value;
                if (options.ReportNonManifoldEdges && uses.Count > 2)
                {
                    issues.Add(new MeshValidationIssue2D(
                        MeshValidationSeverity2D.Warning,
                        "NonManifoldEdge",
                        $"Edge is shared by {uses.Count} faces.",
                        edge: pair.Key));
                }
                else if (options.ReportInconsistentWinding && uses.Count == 2 && uses[0].Direction == uses[1].Direction)
                {
                    issues.Add(new MeshValidationIssue2D(
                        MeshValidationSeverity2D.Warning,
                        "InconsistentWinding",
                        $"Faces {uses[0].FaceIndex} and {uses[1].FaceIndex} use the shared edge in the same direction.",
                        edge: pair.Key));
                }
            }
        }

        return new MeshValidationResult2D(issues);
    }

    private static bool ValidateFaceIndices(Mesh2D mesh, Face2D face, int faceIndex, List<MeshValidationIssue2D> issues)
    {
        var valid = true;
        valid &= ValidateFaceIndex(mesh, face.A, faceIndex, nameof(Face2D.A), issues);
        valid &= ValidateFaceIndex(mesh, face.B, faceIndex, nameof(Face2D.B), issues);
        valid &= ValidateFaceIndex(mesh, face.C, faceIndex, nameof(Face2D.C), issues);
        return valid;
    }

    private static bool ValidateFaceIndex(
        Mesh2D mesh,
        int vertexIndex,
        int faceIndex,
        string cornerName,
        List<MeshValidationIssue2D> issues)
    {
        if ((uint)vertexIndex < (uint)mesh.Vertices.Count) return true;

        issues.Add(new MeshValidationIssue2D(
            MeshValidationSeverity2D.Error,
            "FaceIndexOutOfRange",
            $"{cornerName} references vertex {vertexIndex}, but the mesh has {mesh.Vertices.Count} vertices.",
            vertexIndex,
            faceIndex));
        return false;
    }

    private static void AddEdgeUse(Dictionary<Edge2D, List<EdgeUse>> edgeUses, int from, int to, int faceIndex)
    {
        var edge = new Edge2D(from, to);
        if (!edgeUses.TryGetValue(edge, out var uses))
        {
            uses = new List<EdgeUse>(2);
            edgeUses[edge] = uses;
        }

        uses.Add(new EdgeUse(faceIndex, from <= to ? 1 : -1));
    }

    private static float TriangleArea(Vector2 a, Vector2 b, Vector2 c)
    {
        return MathF.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) * 0.5f;
    }

    private readonly struct EdgeUse
    {
        public EdgeUse(int faceIndex, int direction)
        {
            FaceIndex = faceIndex;
            Direction = direction;
        }

        public int FaceIndex { get; }

        public int Direction { get; }
    }

    private readonly struct FaceKey : IEquatable<FaceKey>
    {
        private readonly int _a;
        private readonly int _b;
        private readonly int _c;

        public FaceKey(int a, int b, int c)
        {
            if (a > b) Swap(ref a, ref b);
            if (b > c) Swap(ref b, ref c);
            if (a > b) Swap(ref a, ref b);

            _a = a;
            _b = b;
            _c = c;
        }

        public bool Equals(FaceKey other) => _a == other._a && _b == other._b && _c == other._c;

        public override bool Equals(object? obj) => obj is FaceKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(_a);
            hash.Add(_b);
            hash.Add(_c);
            return hash.ToHashCode();
        }

        private static void Swap(ref int left, ref int right)
        {
            var temp = left;
            left = right;
            right = temp;
        }
    }
}
