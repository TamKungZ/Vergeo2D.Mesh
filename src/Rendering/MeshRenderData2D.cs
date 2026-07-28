namespace Vergeo2D.Rendering;

public sealed class MeshRenderData2D
{
    public const int FloatsPerVertex = 4;

    private float[] _vertexBuffer = Array.Empty<float>();
    private int[] _indexBuffer = Array.Empty<int>();

    public int VertexCount { get; private set; }
    public int IndexCount { get; private set; }

    public bool VerticesDirty { get; private set; } = true;

    public bool IndicesDirty { get; private set; } = true;

    public ReadOnlySpan<float> Vertices => _vertexBuffer.AsSpan(0, VertexCount * FloatsPerVertex);

    public ReadOnlySpan<int> Indices => _indexBuffer.AsSpan(0, IndexCount);

    public void ClearDirtyFlags()
    {
        VerticesDirty = false;
        IndicesDirty = false;
    }

    public void Clear()
    {
        VertexCount = 0;
        IndexCount = 0;
        VerticesDirty = true;
        IndicesDirty = true;
    }

    public Span<float> GetVertexWriteSpan(int vertexCount)
    {
        var requiredLength = vertexCount * FloatsPerVertex;
        if (_vertexBuffer.Length < requiredLength)
        {
            var newSize = Math.Max(requiredLength, Math.Max(_vertexBuffer.Length * 2, 64));
            Array.Resize(ref _vertexBuffer, newSize);
        }

        VertexCount = vertexCount;
        VerticesDirty = true;
        return _vertexBuffer.AsSpan(0, requiredLength);
    }

    public Span<int> GetIndexWriteSpan(int indexCount)
    {
        if (_indexBuffer.Length < indexCount)
        {
            var newSize = Math.Max(indexCount, Math.Max(_indexBuffer.Length * 2, 64));
            Array.Resize(ref _indexBuffer, newSize);
        }

        IndexCount = indexCount;
        IndicesDirty = true;
        return _indexBuffer.AsSpan(0, indexCount);
    }
}

