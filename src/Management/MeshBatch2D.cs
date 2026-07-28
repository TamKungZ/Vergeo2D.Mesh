using Vergeo2D.Mesh;

namespace Vergeo2D.Management;

public sealed class MeshBatch2D
{
    public Texture2D? Texture { get; }
    public IReadOnlyList<MeshHandle> Handles { get; }

    internal MeshBatch2D(Texture2D? texture, IReadOnlyList<MeshHandle> handles)
    {
        Texture = texture;
        Handles = handles;
    }
}

