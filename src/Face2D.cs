namespace Vergeo2D.Mesh;

public readonly struct Face2D
{
    public int A { get; }
    public int B { get; }
    public int C { get; }

    public Face2D(int a, int b, int c)
    {
        A = a;
        B = b;
        C = c;
    }

    public bool Contains(int vertexIndex) => A == vertexIndex || B == vertexIndex || C == vertexIndex;

    public IEnumerable<Edge2D> GetEdges()
    {
        yield return new Edge2D(A, B);
        yield return new Edge2D(B, C);
        yield return new Edge2D(C, A);
    }
}
