#if VERGEO_OPENGL
using Silk.NET.OpenGL;
using Vergeo2D.Mesh;

namespace Vergeo2D.Rendering.Backends;

public sealed class OpenGLMeshRenderBackend2D : IMeshRenderBackend2D
{
    private struct Resource
    {
        public uint Vao;
        public uint Vbo;
        public uint Ebo;
        public int IndexCount;
        public uint TextureId;
        public bool Alive;
        public int Generation;
    }

    private readonly GL _gl;
    private Resource[] _resources = new Resource[64];
    private readonly Stack<int> _free = new();
    private int _highWaterMark;

    public OpenGLMeshRenderBackend2D(GL gl)
    {
        _gl = gl;
    }

    public unsafe RenderResourceHandle CreateResource(MeshRenderData2D data)
    {
        var vao = _gl.GenVertexArray();
        _gl.BindVertexArray(vao);

        var vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        UploadVertexBuffer(data);

        var ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        UploadIndexBuffer(data);

        var stride = (uint)(MeshRenderData2D.FloatsPerVertex * sizeof(float));
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));

        _gl.BindVertexArray(0);

        var index = AllocateSlot();
        _resources[index].Vao = vao;
        _resources[index].Vbo = vbo;
        _resources[index].Ebo = ebo;
        _resources[index].IndexCount = data.IndexCount;
        if (_resources[index].Generation == 0) _resources[index].Generation = 1;
        _resources[index].Alive = true;
        data.ClearDirtyFlags();

        return new RenderResourceHandle(index, _resources[index].Generation);
    }

    public unsafe void UpdateResource(RenderResourceHandle handle, MeshRenderData2D data)
    {
        ref var resource = ref Get(handle);

        if (data.VerticesDirty)
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, resource.Vbo);
            UploadVertexBuffer(data);
        }

        if (data.IndicesDirty)
        {
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, resource.Ebo);
            UploadIndexBuffer(data);
            resource.IndexCount = data.IndexCount;
        }

        data.ClearDirtyFlags();
    }

    public void BindTexture(RenderResourceHandle handle, Texture2D? texture)
    {
        ref var resource = ref Get(handle);
        if (texture is null) resource.TextureId = 0;
    }

    public unsafe void Draw(RenderResourceHandle handle, in RenderTransform2D transform)
    {
        ref var resource = ref Get(handle);

        if (resource.TextureId != 0)
        {
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, resource.TextureId);
        }

        _gl.BindVertexArray(resource.Vao);
        _gl.DrawElements(PrimitiveType.Triangles, (uint)resource.IndexCount, DrawElementsType.UnsignedInt, null);
    }

    public void DestroyResource(RenderResourceHandle handle)
    {
        ref var resource = ref Get(handle);
        _gl.DeleteVertexArray(resource.Vao);
        _gl.DeleteBuffer(resource.Vbo);
        _gl.DeleteBuffer(resource.Ebo);
        resource.Alive = false;
        resource.Generation++;
        _free.Push(handle.Index);
    }

    public void Dispose()
    {
        for (var i = 0; i < _highWaterMark; i++)
        {
            if (!_resources[i].Alive) continue;
            _gl.DeleteVertexArray(_resources[i].Vao);
            _gl.DeleteBuffer(_resources[i].Vbo);
            _gl.DeleteBuffer(_resources[i].Ebo);
        }
    }

    private unsafe void UploadVertexBuffer(MeshRenderData2D data)
    {
        fixed (float* ptr = data.Vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.VertexCount * MeshRenderData2D.FloatsPerVertex * sizeof(float)), ptr, BufferUsageARB.DynamicDraw);
        }
    }

    private unsafe void UploadIndexBuffer(MeshRenderData2D data)
    {
        fixed (int* ptr = data.Indices)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(data.IndexCount * sizeof(int)), ptr, BufferUsageARB.DynamicDraw);
        }
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
