using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Vergeo2D.Rendering;

internal sealed class MeshPreviewRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly uint _shader;
    private readonly uint _texture;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly int _viewportUniform;
    private readonly int _imageOriginUniform;
    private readonly int _imageScaleUniform;
    private int _drawVertexCount;

    public unsafe MeshPreviewRenderer(GL gl, string imagePath, MeshRenderData2D renderData)
    {
        _gl = gl;
        _shader = GlShader.CreateProgram(gl, VertexShaderSource, FragmentShaderSource);
        _viewportUniform = gl.GetUniformLocation(_shader, "uViewport");
        _imageOriginUniform = gl.GetUniformLocation(_shader, "uImageOrigin");
        _imageScaleUniform = gl.GetUniformLocation(_shader, "uImageScale");
        _texture = GlTextureLoader.Load(gl, imagePath);

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();

        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        var vertices = TestMeshFactory.ExpandIndexedTriangles(renderData);
        _drawVertexCount = vertices.Length / MeshRenderData2D.FloatsPerVertex;
        UploadVertices(vertices);

        var stride = (uint)(MeshRenderData2D.FloatsPerVertex * sizeof(float));
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, null);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
        gl.BindVertexArray(0);

        gl.UseProgram(_shader);
        gl.Uniform1(gl.GetUniformLocation(_shader, "uTexture"), 0);
    }

    public void Draw(Vector2D<int> viewport, Vector2 imageOrigin, float imageScale)
    {
        if (_drawVertexCount == 0) return;

        _gl.UseProgram(_shader);
        _gl.Uniform2(_viewportUniform, (float)viewport.X, (float)viewport.Y);
        _gl.Uniform2(_imageOriginUniform, imageOrigin.X, imageOrigin.Y);
        _gl.Uniform1(_imageScaleUniform, imageScale);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_drawVertexCount);
    }

    public void Update(MeshRenderData2D renderData)
    {
        var vertices = TestMeshFactory.ExpandIndexedTriangles(renderData);
        _drawVertexCount = vertices.Length / MeshRenderData2D.FloatsPerVertex;
        UploadVertices(vertices);
    }

    private unsafe void UploadVertices(float[] vertices)
    {
        fixed (float* pointer = vertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                pointer,
                BufferUsageARB.DynamicDraw);
        }
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteTexture(_texture);
        _gl.DeleteProgram(_shader);
    }

    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in vec2 aUv;

        uniform vec2 uViewport;
        uniform vec2 uImageOrigin;
        uniform float uImageScale;

        out vec2 vImagePixel;
        out vec2 vUv;

        void main()
        {
            vec2 screenPixel = (aPosition * uImageScale) + uImageOrigin;
            vec2 ndc = vec2((screenPixel.x / uViewport.x) * 2.0 - 1.0, 1.0 - (screenPixel.y / uViewport.y) * 2.0);
            gl_Position = vec4(ndc, 0.0, 1.0);
            vImagePixel = aPosition * uImageScale;
            vUv = aUv;
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 vImagePixel;
        in vec2 vUv;
        out vec4 FragColor;

        uniform sampler2D uTexture;

        void main()
        {
            vec4 color = texture(uTexture, vUv);
            float square = 16.0;
            float checkerIndex = mod(floor(vImagePixel.x / square) + floor(vImagePixel.y / square), 2.0);
            vec3 checker = mix(vec3(0.70), vec3(0.90), checkerIndex);
            FragColor = vec4(mix(checker, color.rgb, color.a), 1.0);
        }
        """;
}
