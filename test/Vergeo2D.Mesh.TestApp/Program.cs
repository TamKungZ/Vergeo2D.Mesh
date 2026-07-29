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
    private uint _texture;
    private uint _shader;
    private int _drawVertexCount;
    private int _viewportUniform;
    private int _imageOriginUniform;
    private int _imageScaleUniform;
    private Vector2 _imageSize;
    private bool _reportedDrawError;

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
        gl.Disable(EnableCap.CullFace);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.ScissorTest);
        gl.Disable(EnableCap.StencilTest);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var textureInfo = Texture2D.LoadFromFile(_imagePath);
        var mesh = CreateImageMesh(textureInfo);
        MeshRenderExtractor.Extract(mesh, deformer: null, _renderData);
        Console.WriteLine($"Loaded {_imagePath}");
        Console.WriteLine($"Texture: {textureInfo.Width}x{textureInfo.Height}");
        Console.WriteLine($"Render data: {_renderData.VertexCount} vertices, {_renderData.IndexCount} indices");

        _imageSize = new Vector2(textureInfo.Width, textureInfo.Height);
        _shader = CreateShaderProgram(gl);
        _viewportUniform = gl.GetUniformLocation(_shader, "uViewport");
        _imageOriginUniform = gl.GetUniformLocation(_shader, "uImageOrigin");
        _imageScaleUniform = gl.GetUniformLocation(_shader, "uImageScale");
        _texture = LoadTexture(gl, _imagePath);

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();

        gl.BindVertexArray(_vao);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        var vertices = ExpandIndexedTriangles(_renderData);
        _drawVertexCount = vertices.Length / MeshRenderData2D.FloatsPerVertex;
        fixed (float* vertexPointer = vertices)
        {
            gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                vertexPointer,
                BufferUsageARB.StaticDraw);
        }

        var stride = (uint)(MeshRenderData2D.FloatsPerVertex * sizeof(float));
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, null);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));

        gl.BindVertexArray(0);
        CheckGl(gl, "create mesh buffers");
        OnResize(_window.FramebufferSize);
    }

    private unsafe void OnRender(double deltaSeconds)
    {
        var gl = _gl!;
        var viewport = _window.FramebufferSize;
        if (viewport.X <= 0 || viewport.Y <= 0) return;

        gl.Viewport(viewport);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        gl.UseProgram(_shader);
        var imageScale = MathF.Min(viewport.X / _imageSize.X, viewport.Y / _imageSize.Y);
        var scaledImageSize = _imageSize * imageScale;
        var imageOrigin = new Vector2(
            MathF.Round((viewport.X - scaledImageSize.X) * 0.5f),
            MathF.Round((viewport.Y - scaledImageSize.Y) * 0.5f));
        gl.Uniform2(_viewportUniform, (float)viewport.X, (float)viewport.Y);
        gl.Uniform2(_imageOriginUniform, imageOrigin.X, imageOrigin.Y);
        gl.Uniform1(_imageScaleUniform, imageScale);

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _texture);
        gl.BindVertexArray(_vao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_drawVertexCount);
        if (!_reportedDrawError)
            _reportedDrawError = !CheckGl(gl, "draw frame");
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

    private static float[] ExpandIndexedTriangles(MeshRenderData2D renderData)
    {
        var sourceVertices = renderData.Vertices;
        var sourceIndices = renderData.Indices;
        var expanded = new float[sourceIndices.Length * MeshRenderData2D.FloatsPerVertex];

        for (var i = 0; i < sourceIndices.Length; i++)
        {
            var sourceOffset = sourceIndices[i] * MeshRenderData2D.FloatsPerVertex;
            var targetOffset = i * MeshRenderData2D.FloatsPerVertex;

            expanded[targetOffset] = sourceVertices[sourceOffset];
            expanded[targetOffset + 1] = sourceVertices[sourceOffset + 1];
            expanded[targetOffset + 2] = sourceVertices[sourceOffset + 2];
            expanded[targetOffset + 3] = sourceVertices[sourceOffset + 3];
        }

        return expanded;
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
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

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
            """);

        var fragmentShader = CompileShader(gl, ShaderType.FragmentShader, """
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
            """);

        var program = gl.CreateProgram();
        gl.AttachShader(program, vertexShader);
        gl.AttachShader(program, fragmentShader);
        gl.BindFragDataLocation(program, 0, "FragColor");
        gl.LinkProgram(program);

        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var status);
        if (status == 0) throw new InvalidOperationException(gl.GetProgramInfoLog(program));

        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);
        gl.UseProgram(program);
        gl.Uniform1(gl.GetUniformLocation(program, "uTexture"), 0);
        return program;
    }

    private static bool CheckGl(GL gl, string stage)
    {
        var error = gl.GetError();
        if (error != GLEnum.NoError)
        {
            Console.Error.WriteLine($"OpenGL error after {stage}: {error}");
            return false;
        }

        return true;
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
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteTexture(_texture);
        _gl.DeleteProgram(_shader);
    }
}
