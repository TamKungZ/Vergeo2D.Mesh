#if VERGEO_VULKAN
using Silk.NET.Vulkan;
using Vergeo2D.Mesh;

namespace Vergeo2D.Rendering.Backends;

public unsafe sealed class VulkanMeshRenderBackend2D : IMeshRenderBackend2D
{
    private struct Resource
    {
        public Silk.NET.Vulkan.Buffer VertexBuffer;
        public DeviceMemory VertexMemory;
        public ulong VertexCapacityBytes;
        public Silk.NET.Vulkan.Buffer IndexBuffer;
        public DeviceMemory IndexMemory;
        public ulong IndexCapacityBytes;
        public int IndexCount;
        public bool Alive;
        public int Generation;
    }

    private readonly Vk _vk;
    private readonly PhysicalDevice _physicalDevice;
    private readonly Device _device;
    private readonly CommandBuffer _commandBuffer;

    private Resource[] _resources = new Resource[64];
    private readonly Stack<int> _free = new();
    private int _highWaterMark;

    public VulkanMeshRenderBackend2D(Vk vk, PhysicalDevice physicalDevice, Device device, CommandBuffer commandBuffer)
    {
        _vk = vk;
        _physicalDevice = physicalDevice;
        _device = device;
        _commandBuffer = commandBuffer;
    }

    public RenderResourceHandle CreateResource(MeshRenderData2D data)
    {
        var index = AllocateSlot();
        ref var resource = ref _resources[index];

        CreateOrGrowBuffer(ref resource.VertexBuffer, ref resource.VertexMemory, ref resource.VertexCapacityBytes,
            (ulong)(data.VertexCount * MeshRenderData2D.FloatsPerVertex * sizeof(float)), BufferUsageFlags.VertexBufferBit);
        UploadVertexBuffer(resource, data);

        CreateOrGrowBuffer(ref resource.IndexBuffer, ref resource.IndexMemory, ref resource.IndexCapacityBytes,
            (ulong)(data.IndexCount * sizeof(int)), BufferUsageFlags.IndexBufferBit);
        UploadIndexBuffer(resource, data);

        resource.IndexCount = data.IndexCount;
        if (resource.Generation == 0) resource.Generation = 1;
        resource.Alive = true;
        data.ClearDirtyFlags();

        return new RenderResourceHandle(index, resource.Generation);
    }

    public void UpdateResource(RenderResourceHandle handle, MeshRenderData2D data)
    {
        ref var resource = ref Get(handle);

        if (data.VerticesDirty)
        {
            CreateOrGrowBuffer(ref resource.VertexBuffer, ref resource.VertexMemory, ref resource.VertexCapacityBytes,
                (ulong)(data.VertexCount * MeshRenderData2D.FloatsPerVertex * sizeof(float)), BufferUsageFlags.VertexBufferBit);
            UploadVertexBuffer(resource, data);
        }

        if (data.IndicesDirty)
        {
            CreateOrGrowBuffer(ref resource.IndexBuffer, ref resource.IndexMemory, ref resource.IndexCapacityBytes,
                (ulong)(data.IndexCount * sizeof(int)), BufferUsageFlags.IndexBufferBit);
            UploadIndexBuffer(resource, data);
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

        var vertexBuffer = resource.VertexBuffer;
        ulong offset = 0;
        _vk.CmdBindVertexBuffers(_commandBuffer, 0, 1, ref vertexBuffer, ref offset);
        _vk.CmdBindIndexBuffer(_commandBuffer, resource.IndexBuffer, 0, IndexType.Uint32);
        _vk.CmdDrawIndexed(_commandBuffer, (uint)resource.IndexCount, 1, 0, 0, 0);
    }

    public void DestroyResource(RenderResourceHandle handle)
    {
        ref var resource = ref Get(handle);
        _vk.DestroyBuffer(_device, resource.VertexBuffer, null);
        _vk.FreeMemory(_device, resource.VertexMemory, null);
        _vk.DestroyBuffer(_device, resource.IndexBuffer, null);
        _vk.FreeMemory(_device, resource.IndexMemory, null);
        resource.Alive = false;
        resource.Generation++;
        _free.Push(handle.Index);
    }

    public void Dispose()
    {
        for (var i = 0; i < _highWaterMark; i++)
        {
            if (!_resources[i].Alive) continue;
            _vk.DestroyBuffer(_device, _resources[i].VertexBuffer, null);
            _vk.FreeMemory(_device, _resources[i].VertexMemory, null);
            _vk.DestroyBuffer(_device, _resources[i].IndexBuffer, null);
            _vk.FreeMemory(_device, _resources[i].IndexMemory, null);
        }
    }

    private void CreateOrGrowBuffer(ref Silk.NET.Vulkan.Buffer buffer, ref DeviceMemory memory, ref ulong capacityBytes, ulong requiredBytes, BufferUsageFlags usage)
    {
        if (requiredBytes <= capacityBytes && buffer.Handle != default) return;

        if (buffer.Handle != default)
        {
            _vk.DestroyBuffer(_device, buffer, null);
            _vk.FreeMemory(_device, memory, null);
        }

        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = requiredBytes,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        if (_vk.CreateBuffer(_device, in bufferInfo, null, out buffer) != Result.Success)
            throw new InvalidOperationException("Failed to create Vulkan buffer.");

        _vk.GetBufferMemoryRequirements(_device, buffer, out var requirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
        };

        if (_vk.AllocateMemory(_device, in allocInfo, null, out memory) != Result.Success)
            throw new InvalidOperationException("Failed to allocate Vulkan buffer memory.");

        _vk.BindBufferMemory(_device, buffer, memory, 0);
        capacityBytes = requiredBytes;
    }

    private void UploadVertexBuffer(Resource resource, MeshRenderData2D data)
    {
        var byteLength = data.VertexCount * MeshRenderData2D.FloatsPerVertex * sizeof(float);
        void* mapped;
        _vk.MapMemory(_device, resource.VertexMemory, 0, (ulong)byteLength, 0, &mapped);
        fixed (float* source = data.Vertices)
        {
            System.Buffer.MemoryCopy(source, mapped, byteLength, byteLength);
        }
        _vk.UnmapMemory(_device, resource.VertexMemory);
    }

    private void UploadIndexBuffer(Resource resource, MeshRenderData2D data)
    {
        var byteLength = data.IndexCount * sizeof(int);
        void* mapped;
        _vk.MapMemory(_device, resource.IndexMemory, 0, (ulong)byteLength, 0, &mapped);
        fixed (int* source = data.Indices)
        {
            System.Buffer.MemoryCopy(source, mapped, byteLength, byteLength);
        }
        _vk.UnmapMemory(_device, resource.IndexMemory);
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memoryProperties);

        for (var i = 0; i < memoryProperties.MemoryTypeCount; i++)
        {
            var matchesFilter = (typeFilter & (1u << i)) != 0;
            var matchesProperties = (memoryProperties.MemoryTypes[i].PropertyFlags & properties) == properties;
            if (matchesFilter && matchesProperties) return (uint)i;
        }

        throw new InvalidOperationException("No suitable Vulkan memory type found.");
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
