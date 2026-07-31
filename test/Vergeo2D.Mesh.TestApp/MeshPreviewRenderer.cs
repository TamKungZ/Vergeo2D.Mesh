using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using StbImageSharp;
using Veldrid;
using Veldrid.SPIRV;
using Vergeo2D.Rendering;

internal sealed class MeshPreviewRenderer : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ResourceFactory _factory;
    private readonly Pipeline _pipeline;
    private readonly ResourceLayout _uniformLayout;
    private readonly ResourceLayout _textureLayout;
    private DeviceBuffer _vertexBuffer;
    private readonly DeviceBuffer _uniformBuffer;
    private readonly Texture _texture;
    private readonly TextureView _textureView;
    private readonly Sampler _sampler;
    private readonly ResourceSet _uniformSet;
    private readonly ResourceSet _textureSet;
    private int _drawVertexCount;

    public MeshPreviewRenderer(GraphicsDevice graphicsDevice, string imagePath, MeshRenderData2D renderData)
    {
        _graphicsDevice = graphicsDevice;
        _factory = graphicsDevice.ResourceFactory;

        _uniformLayout = _factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("Projection", ResourceKind.UniformBuffer, ShaderStages.Vertex)));
        _textureLayout = _factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("SurfaceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("SurfaceSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

        var shaders = _factory.CreateFromSpirv(
            new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexShaderSource), "main"),
            new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentShaderSource), "main"));

        var vertexLayout = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
            new VertexElementDescription("UV", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2));

        _pipeline = _factory.CreateGraphicsPipeline(new GraphicsPipelineDescription
        {
            BlendState = BlendStateDescription.SingleAlphaBlend,
            DepthStencilState = DepthStencilStateDescription.Disabled,
            RasterizerState = RasterizerStateDescription.CullNone,
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            ResourceLayouts = new[] { _uniformLayout, _textureLayout },
            ShaderSet = new ShaderSetDescription(new[] { vertexLayout }, shaders),
            Outputs = graphicsDevice.SwapchainFramebuffer.OutputDescription
        });

        foreach (var shader in shaders) shader.Dispose();

        _vertexBuffer = _factory.CreateBuffer(new BufferDescription(4, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        _uniformBuffer = _factory.CreateBuffer(new BufferDescription(32, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        _texture = LoadTexture(imagePath);
        _textureView = _factory.CreateTextureView(_texture);
        _sampler = _factory.CreateSampler(SamplerDescription.Linear);
        _uniformSet = _factory.CreateResourceSet(new ResourceSetDescription(_uniformLayout, _uniformBuffer));
        _textureSet = _factory.CreateResourceSet(new ResourceSetDescription(_textureLayout, _textureView, _sampler));

        Update(renderData);
    }

    public void Draw(CommandList commandList, Vector2 viewport, Vector2 imageOrigin, float imageScale)
    {
        if (_drawVertexCount == 0) return;

        Span<float> projection = stackalloc[]
        {
            viewport.X, viewport.Y, imageOrigin.X, imageOrigin.Y,
            imageScale, 0f, 0f, 0f
        };
        commandList.UpdateBuffer(_uniformBuffer, 0, projection);

        commandList.SetPipeline(_pipeline);
        commandList.SetGraphicsResourceSet(0, _uniformSet);
        commandList.SetGraphicsResourceSet(1, _textureSet);
        commandList.SetVertexBuffer(0, _vertexBuffer);
        commandList.Draw((uint)_drawVertexCount);
    }

    public void Update(MeshRenderData2D renderData)
    {
        var source = renderData.ExpandIndexedTriangles();
        var vertices = new VeldridVertex[source.Length / MeshRenderData2D.FloatsPerVertex];
        for (var i = 0; i < vertices.Length; i++)
        {
            var offset = i * MeshRenderData2D.FloatsPerVertex;
            vertices[i] = new VeldridVertex(
                new Vector2(source[offset], source[offset + 1]),
                new Vector2(source[offset + 2], source[offset + 3]));
        }

        _drawVertexCount = vertices.Length;
        if (vertices.Length == 0) return;

        var requiredBytes = (uint)(vertices.Length * Unsafe.SizeOf<VeldridVertex>());
        EnsureVertexCapacity(requiredBytes);

        _graphicsDevice.UpdateBuffer(_vertexBuffer, 0, vertices);
    }

    private void EnsureVertexCapacity(uint requiredBytes)
    {
        if (_vertexBuffer.SizeInBytes >= requiredBytes) return;

        _vertexBuffer.Dispose();
        _vertexBuffer = _factory.CreateBuffer(new BufferDescription(requiredBytes, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
    }

    private unsafe Texture LoadTexture(string imagePath)
    {
        using var stream = File.OpenRead(imagePath);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        var texture = _factory.CreateTexture(TextureDescription.Texture2D(
            (uint)image.Width,
            (uint)image.Height,
            mipLevels: 1,
            arrayLayers: 1,
            PixelFormat.R8_G8_B8_A8_UNorm,
            TextureUsage.Sampled));

        fixed (byte* pixels = image.Data)
        {
            _graphicsDevice.UpdateTexture(
                texture,
                (IntPtr)pixels,
                (uint)image.Data.Length,
                0,
                0,
                0,
                (uint)image.Width,
                (uint)image.Height,
                1,
                0,
                0);
        }

        return texture;
    }

    public void Dispose()
    {
        _textureSet.Dispose();
        _uniformSet.Dispose();
        _sampler.Dispose();
        _textureView.Dispose();
        _texture.Dispose();
        _uniformBuffer.Dispose();
        _vertexBuffer.Dispose();
        _pipeline.Dispose();
        _textureLayout.Dispose();
        _uniformLayout.Dispose();
    }

    private const string VertexShaderSource = """
        #version 450

        layout(set = 0, binding = 0) uniform Projection
        {
            vec4 ViewportOrigin;
            vec4 Scale;
        };

        layout(location = 0) in vec2 Position;
        layout(location = 1) in vec2 UV;
        layout(location = 0) out vec2 fsin_UV;

        void main()
        {
            vec2 screenPixel = (Position * Scale.x) + ViewportOrigin.zw;
            vec2 ndc = vec2((screenPixel.x / ViewportOrigin.x) * 2.0 - 1.0, 1.0 - (screenPixel.y / ViewportOrigin.y) * 2.0);
            gl_Position = vec4(ndc, 0.0, 1.0);
            fsin_UV = UV;
        }
        """;

    private const string FragmentShaderSource = """
        #version 450

        layout(set = 1, binding = 0) uniform texture2D SurfaceTexture;
        layout(set = 1, binding = 1) uniform sampler SurfaceSampler;
        layout(location = 0) in vec2 fsin_UV;
        layout(location = 0) out vec4 fsout_Color;

        void main()
        {
            fsout_Color = texture(sampler2D(SurfaceTexture, SurfaceSampler), fsin_UV);
        }
        """;
}
