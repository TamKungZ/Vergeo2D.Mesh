using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;

internal sealed class Solid2DRenderer : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ResourceFactory _factory;
    private readonly Pipeline _trianglePipeline;
    private readonly Pipeline _linePipeline;
    private readonly Pipeline _pointPipeline;
    private readonly ResourceLayout _uniformLayout;
    private DeviceBuffer _vertexBuffer;
    private readonly DeviceBuffer _uniformBuffer;
    private readonly ResourceSet _uniformSet;
    private CommandList? _commandList;
    private Vector2 _viewport;

    public Solid2DRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _factory = graphicsDevice.ResourceFactory;
        _uniformLayout = _factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("SolidProjection", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment)));

        var shaders = _factory.CreateFromSpirv(
            new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexShaderSource), "main"),
            new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentShaderSource), "main"));

        var vertexLayout = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2));

        _trianglePipeline = CreatePipeline(shaders, vertexLayout, PrimitiveTopology.TriangleList);
        _linePipeline = CreatePipeline(shaders, vertexLayout, PrimitiveTopology.LineList);
        _pointPipeline = CreatePipeline(shaders, vertexLayout, PrimitiveTopology.PointList);

        foreach (var shader in shaders) shader.Dispose();

        _vertexBuffer = _factory.CreateBuffer(new BufferDescription(4, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        _uniformBuffer = _factory.CreateBuffer(new BufferDescription(32, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        _uniformSet = _factory.CreateResourceSet(new ResourceSetDescription(_uniformLayout, _uniformBuffer));
    }

    public void Begin(CommandList commandList, Vector2 viewport)
    {
        _commandList = commandList;
        _viewport = viewport;
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
        Draw(PrimitiveTopology.TriangleList, vertices, color);
    }

    public void DrawLineLoop(float[] vertices, Vector4 color)
    {
        if (vertices.Length < 4) return;

        var lineVertices = new float[vertices.Length * 2];
        var target = 0;
        var count = vertices.Length / 2;
        for (var i = 0; i < count; i++)
        {
            var next = (i + 1) % count;
            lineVertices[target++] = vertices[i * 2];
            lineVertices[target++] = vertices[i * 2 + 1];
            lineVertices[target++] = vertices[next * 2];
            lineVertices[target++] = vertices[next * 2 + 1];
        }

        DrawLines(lineVertices, color);
    }

    public void DrawPoints(float[] vertices, Vector4 color)
    {
        Draw(PrimitiveTopology.PointList, vertices, color);
    }

    public void DrawLines(float[] vertices, Vector4 color, bool lineStrip = false)
    {
        if (!lineStrip)
        {
            Draw(PrimitiveTopology.LineList, vertices, color);
            return;
        }

        if (vertices.Length < 4) return;

        var lineVertices = new float[(vertices.Length / 2 - 1) * 4];
        var target = 0;
        for (var i = 0; i < vertices.Length / 2 - 1; i++)
        {
            lineVertices[target++] = vertices[i * 2];
            lineVertices[target++] = vertices[i * 2 + 1];
            lineVertices[target++] = vertices[(i + 1) * 2];
            lineVertices[target++] = vertices[(i + 1) * 2 + 1];
        }

        Draw(PrimitiveTopology.LineList, lineVertices, color);
    }

    private Pipeline CreatePipeline(Shader[] shaders, VertexLayoutDescription vertexLayout, PrimitiveTopology topology)
    {
        return _factory.CreateGraphicsPipeline(new GraphicsPipelineDescription
        {
            BlendState = BlendStateDescription.SingleAlphaBlend,
            DepthStencilState = DepthStencilStateDescription.Disabled,
            RasterizerState = RasterizerStateDescription.CullNone,
            PrimitiveTopology = topology,
            ResourceLayouts = new[] { _uniformLayout },
            ShaderSet = new ShaderSetDescription(new[] { vertexLayout }, shaders),
            Outputs = _graphicsDevice.SwapchainFramebuffer.OutputDescription
        });
    }

    private void Draw(PrimitiveTopology topology, float[] vertices, Vector4 color)
    {
        if (_commandList is null || vertices.Length == 0) return;

        var vertexCount = vertices.Length / 2;
        var requiredBytes = (uint)(vertices.Length * sizeof(float));
        EnsureVertexCapacity(requiredBytes);

        Span<float> uniform = stackalloc[]
        {
            _viewport.X, _viewport.Y, 0f, 0f,
            color.X, color.Y, color.Z, color.W
        };
        _commandList.UpdateBuffer(_uniformBuffer, 0, uniform);
        _commandList.UpdateBuffer(_vertexBuffer, 0, vertices);

        _commandList.SetPipeline(topology switch
        {
            PrimitiveTopology.TriangleList => _trianglePipeline,
            PrimitiveTopology.PointList => _pointPipeline,
            _ => _linePipeline
        });
        _commandList.SetGraphicsResourceSet(0, _uniformSet);
        _commandList.SetVertexBuffer(0, _vertexBuffer);
        _commandList.Draw((uint)vertexCount);
    }

    private void EnsureVertexCapacity(uint requiredBytes)
    {
        if (_vertexBuffer.SizeInBytes >= requiredBytes) return;

        _vertexBuffer.Dispose();
        _vertexBuffer = _factory.CreateBuffer(new BufferDescription(requiredBytes, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
    }

    public void Dispose()
    {
        _uniformSet.Dispose();
        _uniformBuffer.Dispose();
        _vertexBuffer.Dispose();
        _pointPipeline.Dispose();
        _linePipeline.Dispose();
        _trianglePipeline.Dispose();
        _uniformLayout.Dispose();
    }

    private const string VertexShaderSource = """
        #version 450

        layout(set = 0, binding = 0) uniform SolidProjection
        {
            vec4 Viewport;
            vec4 Color;
        };

        layout(location = 0) in vec2 Position;

        void main()
        {
            vec2 ndc = vec2((Position.x / Viewport.x) * 2.0 - 1.0, 1.0 - (Position.y / Viewport.y) * 2.0);
            gl_Position = vec4(ndc, 0.0, 1.0);
            gl_PointSize = 8.0;
        }
        """;

    private const string FragmentShaderSource = """
        #version 450

        layout(set = 0, binding = 0) uniform SolidProjection
        {
            vec4 Viewport;
            vec4 Color;
        };

        layout(location = 0) out vec4 fsout_Color;

        void main()
        {
            fsout_Color = Color;
        }
        """;
}
