using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Vergeo2D.Mesh;
using Vergeo2D.Rendering;

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
    private MeshPreviewRenderer? _preview;
    private Solid2DRenderer? _solid;
    private UvOverlayRenderer? _uvOverlay;
    private IInputContext? _input;
    private Vector2 _imageSize;
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

    private void OnLoad()
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
        var mesh = TestMeshFactory.CreateImageMesh(textureInfo);
        MeshRenderExtractor.Extract(mesh, deformer: null, _renderData);
        Console.WriteLine($"Loaded {_imagePath}");
        Console.WriteLine($"Texture: {textureInfo.Width}x{textureInfo.Height}");
        Console.WriteLine($"Render data: {_renderData.VertexCount} vertices, {_renderData.IndexCount} indices");

        _imageSize = new Vector2(textureInfo.Width, textureInfo.Height);
        _preview = new MeshPreviewRenderer(gl, _imagePath, _renderData);
        _solid = new Solid2DRenderer(gl);
        _uvOverlay = new UvOverlayRenderer(_renderData, _imageSize);

        CheckGl(gl, "create render resources");
        OnResize(_window.FramebufferSize);
    }

    private void OnRender(double deltaSeconds)
    {
        var gl = _gl!;
        var viewport = _window.FramebufferSize;
        if (viewport.X <= 0 || viewport.Y <= 0) return;

        gl.Viewport(viewport);
        gl.Clear(ClearBufferMask.ColorBufferBit);

        var imageScale = MathF.Min(viewport.X / _imageSize.X, viewport.Y / _imageSize.Y);
        var scaledImageSize = _imageSize * imageScale;
        var imageOrigin = new Vector2(
            MathF.Round((viewport.X - scaledImageSize.X) * 0.5f),
            MathF.Round((viewport.Y - scaledImageSize.Y) * 0.5f));

        _preview!.Draw(viewport, imageOrigin, imageScale);
        DrawUi(viewport, imageOrigin, imageScale);

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

    private void DrawUi(Vector2D<int> viewport, Vector2 imageOrigin, float imageScale)
    {
        var solid = _solid!;
        solid.Begin(viewport);
        solid.DrawRect(CheckboxOrigin, new Vector2(CheckboxSize, CheckboxSize), new Vector4(0.18f, 0.20f, 0.22f, 0.95f));
        solid.DrawLineLoop(CheckboxVertices(), new Vector4(0.85f, 0.88f, 0.90f, 1f));

        if (!_showUvOverlay) return;

        DrawCheckMark(solid);
        _uvOverlay!.Draw(solid, imageOrigin, imageScale);
    }

    private static void DrawCheckMark(Solid2DRenderer solid)
    {
        var x = CheckboxOrigin.X;
        var y = CheckboxOrigin.Y;
        solid.DrawLines(new[]
        {
            x + 4f, y + 9f,
            x + 8f, y + 13f,
            x + 14f, y + 5f
        }, new Vector4(0.25f, 0.95f, 0.45f, 1f), lineStrip: true);
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

    public void Dispose()
    {
        _preview?.Dispose();
        _solid?.Dispose();
        _input?.Dispose();
    }
}
