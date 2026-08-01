namespace Vergeo2D.Mesh;

public sealed class MeshValidationOptions2D
{
    public float MinimumTriangleArea { get; set; } = 0.0001f;

    public bool ReportDuplicateFaces { get; set; } = true;

    public bool ReportOrphanVertices { get; set; } = true;

    public bool ReportNonManifoldEdges { get; set; } = true;

    public bool ReportInconsistentWinding { get; set; } = true;
}
