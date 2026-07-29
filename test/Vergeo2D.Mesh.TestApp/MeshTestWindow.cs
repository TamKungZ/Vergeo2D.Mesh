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
    private const float PanelWidth = 280f;
    private static readonly Vector2 OverlayCheckboxOrigin = new(18f, 58f);
    private static readonly Vector2 IncludeTransparentCheckboxOrigin = new(18f, 218f);
    private static readonly Vector2 SpacingMinusOrigin = new(18f, 158f);
    private static readonly Vector2 SpacingPlusOrigin = new(110f, 158f);
    private static readonly Vector2 GenerateButtonOrigin = new(18f, 268f);
    private const float CheckboxSize = 18f;
    private static readonly Vector2 StepperButtonSize = new(30f, 24f);
    private static readonly Vector2 GenerateButtonSize = new(150f, 28f);

    private readonly IWindow _window;
    private readonly string _imagePath;
    private readonly MeshRenderData2D _renderData = new();
    private readonly MeshGenerationSettings _generationSettings = new();
    private readonly RadialDragDeformer2D _dragDeformer = new();

    private GL? _gl;
    private MeshPreviewRenderer? _preview;
    private Solid2DRenderer? _solid;
    private UvOverlayRenderer? _uvOverlay;
    private IInputContext? _input;
    private Vector2 _imageSize;
    private Texture2D? _textureInfo;
    private ImageAlphaMask? _alphaMask;
    private Mesh2D? _mesh;
    private Vector2 _dragOriginImage;
    private bool _showUvOverlay;
    private bool _isDragging;
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
        {
            mouse.MouseDown += OnMouseDown;
            mouse.MouseMove += OnMouseMove;
            mouse.MouseUp += OnMouseUp;
        }

        _textureInfo = Texture2D.LoadFromFile(_imagePath);
        _alphaMask = ImageAlphaMask.Load(_imagePath);
        Console.WriteLine($"Loaded {_imagePath}");
        Console.WriteLine($"Texture: {_textureInfo.Width}x{_textureInfo.Height}");

        _imageSize = new Vector2(_textureInfo.Width, _textureInfo.Height);
        _solid = new Solid2DRenderer(gl);
        GenerateMesh();

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

        var layout = GetImageLayout();

        if (_generationSettings.PreviewTransparent)
            DrawCheckerboard(viewport, layout.Origin, _imageSize * layout.Scale, layout.Scale);

        _preview?.Draw(viewport, layout.Origin, layout.Scale);
        DrawUi(viewport, layout.Origin, layout.Scale);

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
        var point = new Vector2(position.X, position.Y);

        if (Contains(point, OverlayCheckboxOrigin, new Vector2(CheckboxSize + 130f, CheckboxSize)))
        {
            _showUvOverlay = !_showUvOverlay;
            return;
        }

        if (Contains(point, IncludeTransparentCheckboxOrigin, new Vector2(CheckboxSize + 190f, CheckboxSize)))
        {
            _generationSettings.PreviewTransparent = !_generationSettings.PreviewTransparent;
            return;
        }

        if (Contains(point, SpacingMinusOrigin, StepperButtonSize))
        {
            _generationSettings.Spacing = Math.Max(4, _generationSettings.Spacing - 4);
            return;
        }

        if (Contains(point, SpacingPlusOrigin, StepperButtonSize))
        {
            _generationSettings.Spacing = Math.Min(512, _generationSettings.Spacing + 4);
            return;
        }

        if (Contains(point, GenerateButtonOrigin, GenerateButtonSize))
        {
            GenerateMesh();
            return;
        }

        BeginImageDrag(point);
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (!_isDragging) return;

        var imagePoint = ScreenToImage(position);
        _dragDeformer.SetDrag(_dragOriginImage, imagePoint - _dragOriginImage);
        RefreshDeformedMesh();
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button != MouseButton.Left) return;

        _isDragging = false;
        CommitDragToMesh();
    }

    private void DrawUi(Vector2D<int> viewport, Vector2 imageOrigin, float imageScale)
    {
        var solid = _solid!;
        solid.Begin(viewport);
        DrawPanel(solid, viewport.Y);
        DrawCheckbox(solid, OverlayCheckboxOrigin, _showUvOverlay);
        BitmapTextRenderer.Draw(solid, "UV OVERLAY", OverlayCheckboxOrigin + new Vector2(28f, 2f), 2f, new Vector4(0.9f, 0.92f, 0.94f, 1f));

        BitmapTextRenderer.Draw(solid, "GENERATOR", new Vector2(18f, 112f), 2f, new Vector4(0.72f, 0.78f, 0.84f, 1f));
        BitmapTextRenderer.Draw(solid, "SPACING", new Vector2(18f, 136f), 2f, new Vector4(0.9f, 0.92f, 0.94f, 1f));
        DrawButton(solid, SpacingMinusOrigin, StepperButtonSize, "-");
        DrawButton(solid, SpacingPlusOrigin, StepperButtonSize, "+");
        BitmapTextRenderer.Draw(solid, _generationSettings.Spacing.ToString(), new Vector2(58f, 163f), 2f, new Vector4(0.9f, 0.92f, 0.94f, 1f));

        DrawCheckbox(solid, IncludeTransparentCheckboxOrigin, _generationSettings.PreviewTransparent);
        BitmapTextRenderer.Draw(solid, "PREVIEW TRANSPARENT", IncludeTransparentCheckboxOrigin + new Vector2(28f, 2f), 2f, new Vector4(0.9f, 0.92f, 0.94f, 1f));
        DrawButton(solid, GenerateButtonOrigin, GenerateButtonSize, "GENERATE");

        BitmapTextRenderer.Draw(solid, $"VERTICES: {_renderData.VertexCount}", new Vector2(18f, 326f), 2f, new Vector4(0.72f, 0.78f, 0.84f, 1f));
        BitmapTextRenderer.Draw(solid, $"FACES: {_renderData.IndexCount / 3}", new Vector2(18f, 350f), 2f, new Vector4(0.72f, 0.78f, 0.84f, 1f));

        if (!_showUvOverlay) return;

        DrawCheckMark(solid);
        _uvOverlay!.Draw(solid, imageOrigin, imageScale);
    }

    private static void DrawPanel(Solid2DRenderer solid, int viewportHeight)
    {
        solid.DrawRect(Vector2.Zero, new Vector2(PanelWidth, viewportHeight), new Vector4(0.07f, 0.08f, 0.09f, 0.92f));
        BitmapTextRenderer.Draw(solid, "TEST MESH", new Vector2(18f, 20f), 2f, new Vector4(0.72f, 0.78f, 0.84f, 1f));
    }

    private void DrawCheckerboard(Vector2D<int> viewport, Vector2 origin, Vector2 size, float imageScale)
    {
        var solid = _solid!;
        var squareSize = Math.Max(4f, MathF.Round(16f * imageScale));
        var columns = (int)MathF.Ceiling(size.X / squareSize);
        var rows = (int)MathF.Ceiling(size.Y / squareSize);
        var light = new Vector4(0.78f, 0.78f, 0.78f, 1f);
        var dark = new Vector4(0.64f, 0.64f, 0.64f, 1f);

        solid.Begin(viewport);
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                var rectOrigin = origin + new Vector2(x * squareSize, y * squareSize);
                var rectSize = new Vector2(
                    Math.Min(squareSize, origin.X + size.X - rectOrigin.X),
                    Math.Min(squareSize, origin.Y + size.Y - rectOrigin.Y));
                var color = ((x + y) & 1) == 0 ? light : dark;
                solid.DrawRect(rectOrigin, rectSize, color);
            }
        }
    }

    private static void DrawCheckbox(Solid2DRenderer solid, Vector2 origin, bool isChecked)
    {
        solid.DrawRect(origin, new Vector2(CheckboxSize, CheckboxSize), new Vector4(0.18f, 0.20f, 0.22f, 0.95f));
        solid.DrawLineLoop(RectVertices(origin, new Vector2(CheckboxSize, CheckboxSize)), new Vector4(0.85f, 0.88f, 0.90f, 1f));
        if (isChecked) DrawCheckMark(solid, origin);
    }

    private static void DrawButton(Solid2DRenderer solid, Vector2 origin, Vector2 size, string label)
    {
        solid.DrawRect(origin, size, new Vector4(0.16f, 0.18f, 0.20f, 0.95f));
        solid.DrawLineLoop(RectVertices(origin, size), new Vector4(0.55f, 0.62f, 0.68f, 1f));
        BitmapTextRenderer.Draw(solid, label, origin + new Vector2(8f, 7f), 2f, new Vector4(0.9f, 0.92f, 0.94f, 1f));
    }

    private static void DrawCheckMark(Solid2DRenderer solid)
    {
        DrawCheckMark(solid, OverlayCheckboxOrigin);
    }

    private static void DrawCheckMark(Solid2DRenderer solid, Vector2 origin)
    {
        var x = origin.X;
        var y = origin.Y;
        solid.DrawLines(new[]
        {
            x + 4f, y + 9f,
            x + 8f, y + 13f,
            x + 14f, y + 5f
        }, new Vector4(0.25f, 0.95f, 0.45f, 1f), lineStrip: true);
    }

    private static float[] RectVertices(Vector2 origin, Vector2 size)
    {
        var x = origin.X;
        var y = origin.Y;
        return new[]
        {
            x, y,
            x + size.X, y,
            x + size.X, y + size.Y,
            x, y + size.Y
        };
    }

    private void GenerateMesh()
    {
        if (_gl is null || _textureInfo is null || _alphaMask is null) return;

        _mesh = GridMeshGenerator.Generate(_textureInfo, _alphaMask, _generationSettings);
        _dragDeformer.Clear();
        _dragDeformer.Radius = Math.Max(120f, _generationSettings.Spacing * 3f);

        _renderData.Clear();
        MeshRenderExtractor.Extract(_mesh, deformer: null, _renderData);
        _preview?.Dispose();
        _preview = new MeshPreviewRenderer(_gl, _imagePath, _renderData);
        _uvOverlay = new UvOverlayRenderer(_renderData, _alphaMask, _imageSize);
        Console.WriteLine($"Generated connected mesh: {_renderData.VertexCount} vertices, {_renderData.IndexCount / 3} faces, spacing {_generationSettings.Spacing}");
    }

    private void BeginImageDrag(Vector2 screenPoint)
    {
        if (_mesh is null || _alphaMask is null || screenPoint.X < PanelWidth) return;
        var imagePoint = ScreenToImage(screenPoint);
        if (!IsInsideImage(imagePoint)) return;
        if (!_alphaMask.IsOpaqueAt(imagePoint.X, imagePoint.Y)) return;
        if (_mesh.FindFaceAt(imagePoint) < 0) return;

        _dragOriginImage = imagePoint;
        _dragDeformer.SetDrag(_dragOriginImage, Vector2.Zero);
        _isDragging = true;
        RefreshDeformedMesh();
    }

    private void RefreshDeformedMesh()
    {
        if (_mesh is null || _preview is null) return;

        _renderData.Clear();
        MeshRenderExtractor.Extract(_mesh, _dragDeformer.HasDrag ? _dragDeformer : null, _renderData);
        _preview.Update(_renderData);
        if (_alphaMask is not null)
            _uvOverlay = new UvOverlayRenderer(_renderData, _alphaMask, _imageSize);
    }

    private void CommitDragToMesh()
    {
        if (_mesh is null || !_dragDeformer.HasDrag) return;

        var deformedPositions = _dragDeformer.Deform(_mesh);
        for (var i = 0; i < _mesh.Vertices.Count; i++)
            _mesh.Vertices[i].Position = deformedPositions[i];

        _dragDeformer.Clear();
        RefreshDeformedMesh();
    }

    private Vector2 ScreenToImage(Vector2 screenPoint)
    {
        var layout = GetImageLayout();
        return (screenPoint - layout.Origin) / layout.Scale;
    }

    private bool IsInsideImage(Vector2 imagePoint)
    {
        return
            imagePoint.X >= 0f &&
            imagePoint.Y >= 0f &&
            imagePoint.X <= _imageSize.X &&
            imagePoint.Y <= _imageSize.Y;
    }

    private ImageLayout GetImageLayout()
    {
        var viewport = _window.FramebufferSize;
        var imageScale = MathF.Min(viewport.X / _imageSize.X, viewport.Y / _imageSize.Y);
        var scaledImageSize = _imageSize * imageScale;
        var imageOrigin = new Vector2(
            MathF.Round((viewport.X - scaledImageSize.X) * 0.5f),
            MathF.Round((viewport.Y - scaledImageSize.Y) * 0.5f));

        return new ImageLayout(imageOrigin, imageScale);
    }

    private static bool Contains(Vector2 point, Vector2 origin, Vector2 size)
    {
        return
            point.X >= origin.X &&
            point.X <= origin.X + size.X &&
            point.Y >= origin.Y &&
            point.Y <= origin.Y + size.Y;
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

    private readonly record struct ImageLayout(Vector2 Origin, float Scale);
}
