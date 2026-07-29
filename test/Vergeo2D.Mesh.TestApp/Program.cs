using System.Numerics;
using Silk.NET.Input;
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
options.Size = new Vector2D<int>(1280, 720);
options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3));

using var app = new MeshTestWindow(options, imagePath);
app.Run();
return 0;

internal sealed class MeshTestWindow : IDisposable
{
    private const int MinimumWidth = 1280;
    private const int MinimumHeight = 720;
    private static readonly Vector2 CheckboxOrigin = new(16f, 18f);
    private const float CheckboxSize = 18f;

    private readonly IWindow _window;
    private readonly string _imagePath;
    private readonly MeshRenderData2D _renderData = new();

    private GL? _gl;
    private uint _vao;
    private uint _vbo;
    private uint _texture;
    private uint _shader;
    private uint _solidVao;
    private uint _solidVbo;
    private uint _solidShader;
    private int _drawVertexCount;
    private int _viewportUniform;
    private int _imageOriginUniform;
    private int _imageScaleUniform;
    private int _solidViewportUniform;
    private int _solidColorUniform;
    private Vector2 _imageSize;
    private IInputContext? _input;
    private bool _showUvOverlay;
    private bool _reportedDrawError;

    public MeshTestWindow(WindowOptions options, string imagePath)
    {
        _imagePath = imagePath;
        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Resize += OnWindowResize;
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
        gl.LineWidth(1f);
        gl.PointSize(8f);

        _input = _window.CreateInput();
        foreach (var mouse in _input.Mice)
            mouse.MouseDown += OnMouseDown;

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
        _solidShader = CreateSolidShaderProgram(gl);
        _solidViewportUniform = gl.GetUniformLocation(_solidShader, "uViewport");
        _solidColorUniform = gl.GetUniformLocation(_solidShader, "uColor");
        _texture = LoadTexture(gl, _imagePath);

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        _solidVao = gl.GenVertexArray();
        _solidVbo = gl.GenBuffer();

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

        gl.BindVertexArray(_solidVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _solidVbo);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), null);
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

        DrawUi(gl, viewport, imageOrigin, imageScale);

        if (!_reportedDrawError)
            _reportedDrawError = !CheckGl(gl, "draw frame");
    }

    private void OnWindowResize(Vector2D<int> size)
    {
        var width = Math.Max(size.X, MinimumWidth);
        var height = Math.Max(size.Y, MinimumHeight);
        if (width != size.X || height != size.Y)
            _window.Size = new Vector2D<int>(width, height);
    }

    private void OnResize(Vector2D<int> size)
    {
        _gl?.Viewport(size);
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button != MouseButton.Left) return;

        var position = mouse.Position;
        var inside =
            position.X >= CheckboxOrigin.X &&
            position.X <= CheckboxOrigin.X + CheckboxSize &&
            position.Y >= CheckboxOrigin.Y &&
            position.Y <= CheckboxOrigin.Y + CheckboxSize;

        if (inside) _showUvOverlay = !_showUvOverlay;
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

    private void DrawUi(GL gl, Vector2D<int> viewport, Vector2 imageOrigin, float imageScale)
    {
        gl.UseProgram(_solidShader);
        gl.Uniform2(_solidViewportUniform, (float)viewport.X, (float)viewport.Y);
        gl.BindVertexArray(_solidVao);

        DrawRect(gl, CheckboxOrigin, new Vector2(CheckboxSize, CheckboxSize), new Vector4(0.18f, 0.20f, 0.22f, 0.95f));
        DrawLineLoop(gl, CheckboxVertices(), new Vector4(0.85f, 0.88f, 0.90f, 1f));

        if (_showUvOverlay)
        {
            DrawCheckMark(gl);
            DrawUvOverlay(gl, imageOrigin, imageScale);
        }
    }

    private void DrawUvOverlay(GL gl, Vector2 imageOrigin, float imageScale)
    {
        var sourceVertices = _renderData.Vertices;
        var sourceIndices = _renderData.Indices;
        var faceVertices = new float[sourceIndices.Length * 2];

        for (var i = 0; i < sourceIndices.Length; i++)
        {
            var vertexOffset = sourceIndices[i] * MeshRenderData2D.FloatsPerVertex;
            var uv = new Vector2(sourceVertices[vertexOffset + 2], sourceVertices[vertexOffset + 3]);
            var screen = UvToScreen(uv, imageOrigin, imageScale);
            var targetOffset = i * 2;
            faceVertices[targetOffset] = screen.X;
            faceVertices[targetOffset + 1] = screen.Y;
        }

        DrawTriangles(gl, faceVertices, new Vector4(0.15f, 0.55f, 1f, 0.18f));

        for (var i = 0; i < sourceIndices.Length; i += 3)
        {
            DrawLineLoop(gl, new[]
            {
                faceVertices[i * 2], faceVertices[i * 2 + 1],
                faceVertices[(i + 1) * 2], faceVertices[(i + 1) * 2 + 1],
                faceVertices[(i + 2) * 2], faceVertices[(i + 2) * 2 + 1]
            }, new Vector4(0.05f, 0.45f, 1f, 0.95f));
        }

        DrawPoints(gl, faceVertices, new Vector4(1f, 0.25f, 0.15f, 1f));
    }

    private Vector2 UvToScreen(Vector2 uv, Vector2 imageOrigin, float imageScale)
    {
        return imageOrigin + new Vector2(uv.X * _imageSize.X, uv.Y * _imageSize.Y) * imageScale;
    }

    private void DrawCheckMark(GL gl)
    {
        var x = CheckboxOrigin.X;
        var y = CheckboxOrigin.Y;
        var points = new[]
        {
            x + 4f, y + 9f,
            x + 8f, y + 13f,
            x + 14f, y + 5f
        };
        DrawLines(gl, points, new Vector4(0.25f, 0.95f, 0.45f, 1f), lineStrip: true);
    }

    private static float[] CheckboxVertices()
    {
        var x = CheckboxOrigin.X;
        var y = CheckboxOrigin.Y;
        return new[]
        {
            x, y,
            x + CheckboxSize, y,
            x + CheckboxSize, y + CheckboxSize,
            x, y + CheckboxSize
        };
    }

    private void DrawRect(GL gl, Vector2 origin, Vector2 size, Vector4 color)
    {
        var x = origin.X;
        var y = origin.Y;
        var w = size.X;
        var h = size.Y;
        DrawTriangles(gl, new[]
        {
            x, y,
            x + w, y,
            x + w, y + h,
            x, y,
            x + w, y + h,
            x, y + h
        }, color);
    }

    private void DrawTriangles(GL gl, float[] vertices, Vector4 color)
    {
        UploadSolidVertices(gl, vertices);
        gl.Uniform4(_solidColorUniform, color.X, color.Y, color.Z, color.W);
        gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(vertices.Length / 2));
    }

    private void DrawLineLoop(GL gl, float[] vertices, Vector4 color)
    {
        UploadSolidVertices(gl, vertices);
        gl.Uniform4(_solidColorUniform, color.X, color.Y, color.Z, color.W);
        gl.DrawArrays(PrimitiveType.LineLoop, 0, (uint)(vertices.Length / 2));
    }

    private void DrawPoints(GL gl, float[] vertices, Vector4 color)
    {
        UploadSolidVertices(gl, vertices);
        gl.Uniform4(_solidColorUniform, color.X, color.Y, color.Z, color.W);
        gl.DrawArrays(PrimitiveType.Points, 0, (uint)(vertices.Length / 2));
    }

    private void DrawLines(GL gl, float[] vertices, Vector4 color, bool lineStrip = false)
    {
        UploadSolidVertices(gl, vertices);
        gl.Uniform4(_solidColorUniform, color.X, color.Y, color.Z, color.W);
        gl.DrawArrays(lineStrip ? PrimitiveType.LineStrip : PrimitiveType.Lines, 0, (uint)(vertices.Length / 2));
    }

    private unsafe void UploadSolidVertices(GL gl, float[] vertices)
    {
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _solidVbo);
        fixed (float* pointer = vertices)
        {
            gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                pointer,
                BufferUsageARB.StreamDraw);
        }
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
                vUv = vec2(aUv.x, 1.0 - aUv.y);
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

    private static uint CreateSolidShaderProgram(GL gl)
    {
        var vertexShader = CompileShader(gl, ShaderType.VertexShader, """
            #version 330 core
            layout (location = 0) in vec2 aPosition;

            uniform vec2 uViewport;

            void main()
            {
                vec2 ndc = vec2((aPosition.x / uViewport.x) * 2.0 - 1.0, 1.0 - (aPosition.y / uViewport.y) * 2.0);
                gl_Position = vec4(ndc, 0.0, 1.0);
            }
            """);

        var fragmentShader = CompileShader(gl, ShaderType.FragmentShader, """
            #version 330 core
            out vec4 FragColor;

            uniform vec4 uColor;

            void main()
            {
                FragColor = uColor;
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
        _gl.DeleteBuffer(_solidVbo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteVertexArray(_solidVao);
        _gl.DeleteTexture(_texture);
        _gl.DeleteProgram(_shader);
        _gl.DeleteProgram(_solidShader);
        _input?.Dispose();
    }
}
