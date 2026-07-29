using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

internal sealed class Solid2DRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly uint _shader;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly int _viewportUniform;
    private readonly int _colorUniform;

    public unsafe Solid2DRenderer(GL gl)
    {
        _gl = gl;
        _shader = GlShader.CreateProgram(gl, VertexShaderSource, FragmentShaderSource);
        _viewportUniform = gl.GetUniformLocation(_shader, "uViewport");
        _colorUniform = gl.GetUniformLocation(_shader, "uColor");

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();

        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), null);
        gl.BindVertexArray(0);
    }

    public void Begin(Vector2D<int> viewport)
    {
        _gl.UseProgram(_shader);
        _gl.Uniform2(_viewportUniform, (float)viewport.X, (float)viewport.Y);
        _gl.BindVertexArray(_vao);
    }

    public void DrawRect(Vector2 origin, Vector2 size, Vector4 color)
    {
        var x = origin.X;
        var y = origin.Y;
        var w = size.X;
        var h = size.Y;
        DrawTriangles(new[]
        {
            x, y,
            x + w, y,
            x + w, y + h,
            x, y,
            x + w, y + h,
            x, y + h
        }, color);
    }

    public void DrawTriangles(float[] vertices, Vector4 color)
    {
        Draw(PrimitiveType.Triangles, vertices, color);
    }

    public void DrawLineLoop(float[] vertices, Vector4 color)
    {
        Draw(PrimitiveType.LineLoop, vertices, color);
    }

    public void DrawPoints(float[] vertices, Vector4 color)
    {
        Draw(PrimitiveType.Points, vertices, color);
    }

    public void DrawLines(float[] vertices, Vector4 color, bool lineStrip = false)
    {
        Draw(lineStrip ? PrimitiveType.LineStrip : PrimitiveType.Lines, vertices, color);
    }

    private void Draw(PrimitiveType primitiveType, float[] vertices, Vector4 color)
    {
        UploadVertices(vertices);
        _gl.Uniform4(_colorUniform, color.X, color.Y, color.Z, color.W);
        _gl.DrawArrays(primitiveType, 0, (uint)(vertices.Length / 2));
    }

    private unsafe void UploadVertices(float[] vertices)
    {
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* pointer = vertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                pointer,
                BufferUsageARB.StreamDraw);
        }
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteProgram(_shader);
    }

    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;

        uniform vec2 uViewport;

        void main()
        {
            vec2 ndc = vec2((aPosition.x / uViewport.x) * 2.0 - 1.0, 1.0 - (aPosition.y / uViewport.y) * 2.0);
            gl_Position = vec4(ndc, 0.0, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        out vec4 FragColor;

        uniform vec4 uColor;

        void main()
        {
            FragColor = uColor;
        }
        """;
}
