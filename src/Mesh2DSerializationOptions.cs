namespace Vergeo2D.Mesh;

public sealed class Mesh2DSerializationOptions
{
    public bool WriteIndented { get; set; } = true;

    public bool LoadTexture { get; set; } = true;

    public bool ThrowOnTextureLoadFailure { get; set; }

    public string? TextureBaseDirectory { get; set; }

    public Func<string, Texture2D?>? TextureLoader { get; set; }
}
