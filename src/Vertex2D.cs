using System.Numerics;

namespace Vergeo2D.Mesh;

public sealed class Vertex2D
{
    private readonly Action? _changed;
    private Vector2 _position;
    private Vector2 _uv;

    public int Index { get; set; }

    public Vector2 Position
    {
        get => _position;
        set
        {
            if (_position == value) return;
            _position = value;
            _changed?.Invoke();
        }
    }

    public Vector2 UV
    {
        get => _uv;
        set
        {
            if (_uv == value) return;
            _uv = value;
            _changed?.Invoke();
        }
    }

    public Vertex2D(int index, Vector2 position, Vector2 uv = default)
        : this(index, position, uv, null)
    {
    }

    internal Vertex2D(int index, Vector2 position, Vector2 uv, Action? changed)
    {
        Index = index;
        _position = position;
        _uv = uv;
        _changed = changed;
    }
}
