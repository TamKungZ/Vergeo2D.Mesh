namespace Vergeo2D.Mesh;

public sealed class MeshValidationIssue2D
{
    public MeshValidationIssue2D(
        MeshValidationSeverity2D severity,
        string code,
        string message,
        int vertexIndex = -1,
        int faceIndex = -1,
        Edge2D? edge = null)
    {
        Severity = severity;
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        VertexIndex = vertexIndex;
        FaceIndex = faceIndex;
        Edge = edge;
    }

    public MeshValidationSeverity2D Severity { get; }

    public string Code { get; }

    public string Message { get; }

    public int VertexIndex { get; }

    public int FaceIndex { get; }

    public Edge2D? Edge { get; }

    public override string ToString()
    {
        return $"{Severity} {Code}: {Message}";
    }
}
