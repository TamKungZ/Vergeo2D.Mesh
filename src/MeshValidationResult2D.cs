namespace Vergeo2D.Mesh;

public sealed class MeshValidationResult2D
{
    internal MeshValidationResult2D(List<MeshValidationIssue2D> issues)
    {
        Issues = issues.AsReadOnly();
    }

    public IReadOnlyList<MeshValidationIssue2D> Issues { get; }

    public bool IsValid => !HasErrors;

    public bool HasErrors
    {
        get
        {
            foreach (var issue in Issues)
                if (issue.Severity == MeshValidationSeverity2D.Error)
                    return true;

            return false;
        }
    }

    public bool HasWarnings
    {
        get
        {
            foreach (var issue in Issues)
                if (issue.Severity == MeshValidationSeverity2D.Warning)
                    return true;

            return false;
        }
    }
}
