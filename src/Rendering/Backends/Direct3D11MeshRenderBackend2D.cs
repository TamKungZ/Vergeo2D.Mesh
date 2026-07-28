#if VERGEO_DIRECTX
using Vortice.Direct3D11;
using Vergeo2D.Mesh;

namespace Vergeo2D.Rendering.Backends;

public sealed class Direct3D11MeshRenderBackend2D : IMeshRenderBackend2D
{
    private struct Resource
    {
        public ID3D11Buffer? VertexBuffer;
        public int VertexCapacityBytes;
        public ID3D11Buffer? IndexBuffer;
        public int IndexCapacityBytes;
        public int IndexCount;
        public bool Alive;
        public int Generation;
    }

    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;

    private Resource[] _resources = new Resource[64];
    private readonly Stack<int> _free = new();
    private int _highWaterMark;

    public Direct3D11MeshRenderBackend2D(ID3D11Device device, ID3D11DeviceContext context)
    {
        _device = device;
        _context = context;
    }

    public RenderResourceHandle CreateResource(MeshRenderData2D data)
    {
        var index = AllocateSlot();
        ref var resource = ref _resources[index];

        CreateOrGrowBuffer(ref resource.VertexBuffer, ref resource.VertexCapacityBytes,
            data.VertexCount * MeshRenderData2D.FloatsPerVertex * sizeof(float), BindFlags.VertexBuffer);
        WriteVertexBuffer(resource, data);

        CreateOrGrowBuffer(ref resource.IndexBuffer, ref resource.IndexCapacityBytes,
            data.IndexCount * sizeof(int), BindFlags.IndexBuffer);
        WriteIndexBuffer(resource, data);

        resource.IndexCount = data.IndexCount;
        resource.Alive = true;
        data.ClearDirtyFlags();

        return new RenderResourceHandle(index, resource.Generation);
    }

    public void UpdateResource(RenderResourceHandle handle, MeshRenderData2D data)
    {
        ref var resource = ref Get(handle);

        if (data.VerticesDirty)
        {
            CreateOrGrowBuffer(ref resource.VertexBuffer, ref resource.VertexCapacityBytes,
                data.VertexCount * MeshRenderData2D.FloatsPerVertex * sizeof(float), BindFlags.VertexBuffer);
            WriteVertexBuffer(resource, data);
        }

        if (data.IndicesDirty)
        {
            CreateOrGrowBuffer(ref resource.IndexBuffer, ref resource.IndexCapacityBytes,
                data.IndexCount * sizeof(int), BindFlags.IndexBuffer);
            WriteIndexBuffer(resource, data);
            resource.IndexCount = data.IndexCount;
        }

        data.ClearDirtyFlags();
    }

    public void BindTexture(RenderResourceHandle handle, Texture2D? texture)
    {
    }

    public void Draw(RenderResourceHandle handle, in RenderTransform2D transform)
    {
        ref var resource = ref Get(handle);

        var stride = MeshRenderData2D.FloatsPerVertex * sizeof(float);
        _context.IASetVertexBuffers(0, new[] { resource.VertexBuffer }, new[] { stride }, new[] { 0 });
        _context.IASetIndexBuffer(resource.IndexBuffer, Vortice.DXGI.Format.R32_UInt, 0);
        _context.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
        _context.DrawIndexed(resource.IndexCount, 0, 0);
    }

    public void DestroyResource(RenderResourceHandle handle)
    {
        ref var resource = ref Get(handle);
        resource.VertexBuffer?.Dispose();
        resource.IndexBuffer?.Dispose();
        resource.Alive = false;
        resource.Generation++;
        _free.Push(handle.Index);
    }

    public void Dispose()
    {
        for (var i = 0; i < _highWaterMark; i++)
        {
            if (!_resources[i].Alive) continue;
            _resources[i].VertexBuffer?.Dispose();
            _resources[i].IndexBuffer?.Dispose();
        }
    }

    private void CreateOrGrowBuffer(ref ID3D11Buffer? buffer, ref int capacityBytes, int requiredBytes, BindFlags bindFlags)
    {
        if (buffer is not null && requiredBytes <= capacityBytes) return;

        buffer?.Dispose();

        var description = new BufferDescription
        {
            ByteWidth = requiredBytes,
            Usage = ResourceUsage.Dynamic,
            BindFlags = bindFlags,
            CPUAccessFlags = CpuAccessFlags.Write
        };

        buffer = _device.CreateBuffer(description);
        capacityBytes = requiredBytes;
    }

    private unsafe void WriteVertexBuffer(Resource resource, MeshRenderData2D data)
    {
        var mapped = _context.Map(resource.VertexBuffer, MapMode.WriteDiscard);
        var byteLength = data.VertexCount * MeshRenderData2D.FloatsPerVertex * sizeof(float);
        fixed (float* source = data.Vertices)
        {
            System.Buffer.MemoryCopy(source, (void*)mapped.DataPointer, byteLength, byteLength);
        }
        _context.Unmap(resource.VertexBuffer, 0);
    }

    private unsafe void WriteIndexBuffer(Resource resource, MeshRenderData2D data)
    {
        var mapped = _context.Map(resource.IndexBuffer, MapMode.WriteDiscard);
        var byteLength = data.IndexCount * sizeof(int);
        fixed (int* source = data.Indices)
        {
            System.Buffer.MemoryCopy(source, (void*)mapped.DataPointer, byteLength, byteLength);
        }
        _context.Unmap(resource.IndexBuffer, 0);
    }

    private int AllocateSlot()
    {
        if (_free.Count > 0) return _free.Pop();
        if (_highWaterMark == _resources.Length) Array.Resize(ref _resources, _resources.Length * 2);
        return _highWaterMark++;
    }

    private ref Resource Get(RenderResourceHandle handle)
    {
        if ((uint)handle.Index >= (uint)_highWaterMark || !_resources[handle.Index].Alive || _resources[handle.Index].Generation != handle.Generation)
            throw new ArgumentException("Invalid or stale render resource handle.", nameof(handle));
        return ref _resources[handle.Index];
    }
}
#endif
