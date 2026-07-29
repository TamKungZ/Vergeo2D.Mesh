using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using StbImageSharp;
using Vergeo2D.Mesh;
using Vergeo2D.Rendering;

var imagePath = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "assets", "character-base.png");

if (!File.Exists(imagePath))
{
    Console.Error.WriteLine($"Test image not found: {imagePath}");
    return 1;
}

var options = WindowOptions.Default;
options.Title = "Vergeo2D.Mesh Test Render";
options.Size = new Vector2D<int>(960, 720);
options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3));

using var app = new MeshTestWindow(options, imagePath);
app.Run();
return 0;

internal sealed class MeshTestWindow : IDisposable
{
    private readonly IWindow _window;
    private readonly string _imagePath;
    private readonly MeshRenderData2D _renderData = new();

    private GL? _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private uint _texture;
    private uint _shader;
    private int _indexCount;
    private int _viewportUniform;
    private int _offsetUniform;
    private Vector2 _contentSize;

    public MeshTestWindow(WindowOptions options, string imagePath)
    {
        _imagePath = imagePath;
        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnResize;
        _window.Closing += Dispose;
    }

    public void Run()
    {
        _window.Run();
    }

    private unsafe void OnLoad()
    {
        _gl = GL.GetApi(_window);
        var gl = _gl;

        gl.ClearColor(0.08f, 0.09f, 0.1f, 1f);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var textureInfo = Texture2D.LoadFromFile(_imagePath);
        var mesh = CreateImageMesh(textureInfo);
        MeshRenderExtractor.Extract(mesh, deformer: null, _renderData);

        _contentSize = new Vector2(textureInfo.Width, textureInfo.Height);
        _indexCount = _renderData.IndexCount;
        _shader = CreateShaderProgram(gl);
        _viewportUniform = gl.GetUniformLocation(_shader, "uViewport");
        _offsetUniform = gl.GetUniformLocation(_shader, "uOffset");
        _texture = LoadTexture(gl, _imagePath);

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        _ebo = gl.GenBuffer();

        gl.BindVertexArray(_vao);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* vertices = _renderData.Vertices)
        {
            gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(_renderData.Vertices.Length * sizeof(float)),
                vertices,
                BufferUsageARB.StaticDraw);
        }

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (int* indices = _renderData.Indices)
        {
            gl.BufferData(
                BufferTargetARB.ElementArrayBuffer,
                (nuint)(_renderData.Indices.Length * sizeof(int)),
                indices,
                BufferUsageARB.StaticDraw);
        }

        var stride = (uint)(MeshRenderData2D.FloatsPerVertex * sizeof(float));
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, null);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));

        gl.BindVertexArray(0);
        OnResize(_window.FramebufferSize);
    }

    private unsafe void OnRender(double deltaSeconds)
    {
        var gl = _gl!;
        gl.Clear(ClearBufferMask.ColorBufferBit);
        gl.UseProgram(_shader);

        var viewport = _window.FramebufferSize;
        var scale = MathF.Min(
            MathF.Min(viewport.X / _contentSize.X, viewport.Y / _contentSize.Y),
            1f);
        var drawSize = _contentSize * scale;
        var offset = new Vector2((viewport.X - drawSize.X) * 0.5f, (viewport.Y - drawSize.Y) * 0.5f);

        gl.Uniform2(_viewportUniform, viewport.X, viewport.Y);
        gl.Uniform3(_offsetUniform, offset.X, offset.Y, scale);

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _texture);
        gl.BindVertexArray(_vao);
        gl.DrawElements(PrimitiveType.Triangles, (uint)_indexCount, DrawElementsType.UnsignedInt, null);
    }

    private void OnResize(Vector2D<int> size)
    {
        _gl?.Viewport(size);
    }

    private static Mesh2D CreateImageMesh(Texture2D texture)
    {
        var mesh = new Mesh2D();
        var topLeft = mesh.AddVertex(new Vector2(0, 0));
        var topRight = mesh.AddVertex(new Vector2(texture.Width, 0));
        var bottomRight = mesh.AddVertex(new Vector2(texture.Width, texture.Height));
        var bottomLeft = mesh.AddVertex(new Vector2(0, texture.Height));

        mesh.AddFace(topLeft, topRight, bottomRight);
        mesh.AddFace(topLeft, bottomRight, bottomLeft);
        mesh.SetTexture(texture);
        mesh.GenerateUVsFromPositions();
        return mesh;
    }

    private static unsafe uint LoadTexture(GL gl, string path)
    {
        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        var texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        fixed (byte* pixels = image.Data)
        {
            gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba,
                (uint)image.Width,
                (uint)image.Height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                pixels);
        }

        return texture;
    }

    private static uint CreateShaderProgram(GL gl)
    {
        var vertexShader = CompileShader(gl, ShaderType.VertexShader, """
            #version 330 core
            layout (location = 0) in vec2 aPosition;
            layout (location = 1) in vec2 aUv;

            uniform vec2 uViewport;
            uniform vec3 uOffset;

            out vec2 vUv;

            void main()
            {
                vec2 pixel = (aPosition * uOffset.z) + uOffset.xy;
                vec2 ndc = vec2((pixel.x / uViewport.x) * 2.0 - 1.0, 1.0 - (pixel.y / uViewport.y) * 2.0);
                gl_Position = vec4(ndc, 0.0, 1.0);
                vUv = vec2(aUv.x, 1.0 - aUv.y);
            }
            """);

        var fragmentShader = CompileShader(gl, ShaderType.FragmentShader, """
            #version 330 core
            in vec2 vUv;
            out vec4 FragColor;

            uniform sampler2D uTexture;

            void main()
            {
                FragColor = texture(uTexture, vUv);
            }
            """);

        var program = gl.CreateProgram();
        gl.AttachShader(program, vertexShader);
        gl.AttachShader(program, fragmentShader);
        gl.LinkProgram(program);

        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var status);
        if (status == 0) throw new InvalidOperationException(gl.GetProgramInfoLog(program));

        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);
        gl.UseProgram(program);
        gl.Uniform1(gl.GetUniformLocation(program, "uTexture"), 0);
        return program;
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);

        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status == 0) throw new InvalidOperationException(gl.GetShaderInfoLog(shader));

        return shader;
    }

    public void Dispose()
    {
        if (_gl is null) return;

        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteTexture(_texture);
        _gl.DeleteProgram(_shader);
    }
}
