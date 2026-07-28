using Vergeo2D.Mesh;
using Vergeo2D.Rendering;

namespace Vergeo2D.Management;

public sealed class MeshManager2D
{
    private Mesh2D?[] _meshes;
    private IMeshDeformer2D?[] _deformers;
    private MeshRenderData2D?[] _renderData;
    private int[] _generations;
    private int[] _lastExtractedVersion;
    private bool[] _alive;
    private bool[] _forceDirty;

    private readonly Stack<int> _freeIndices = new();
    private int _highWaterMark;

    public MeshManager2D(int initialCapacity = 256)
    {
        var capacity = Math.Max(initialCapacity, 16);
        _meshes = new Mesh2D?[capacity];
        _deformers = new IMeshDeformer2D?[capacity];
        _renderData = new MeshRenderData2D?[capacity];
        _generations = new int[capacity];
        _lastExtractedVersion = new int[capacity];
        _alive = new bool[capacity];
        _forceDirty = new bool[capacity];
    }

    public int Count { get; private set; }

    public int Capacity => _meshes.Length;

    public MeshHandle Add(Mesh2D mesh, IMeshDeformer2D? deformer = null)
    {
        if (mesh is null) throw new ArgumentNullException(nameof(mesh));

        var index = AllocateSlot();
        var generation = _generations[index] + 1;
        if (generation == 0) generation = 1;

        _generations[index] = generation;
        _meshes[index] = mesh;
        _deformers[index] = deformer;
        _renderData[index] ??= new MeshRenderData2D();
        _lastExtractedVersion[index] = int.MinValue;
        _alive[index] = true;
        _forceDirty[index] = false;
        Count++;

        return new MeshHandle(index, generation);
    }

    public bool Remove(MeshHandle handle)
    {
        if (!IsValid(handle)) return false;

        var index = handle.Index;
        _meshes[index] = null;
        _deformers[index] = null;
        _renderData[index]?.Clear();
        _alive[index] = false;
        _freeIndices.Push(index);
        Count--;
        return true;
    }

    public void Clear()
    {
        for (var i = 0; i < _highWaterMark; i++)
        {
            if (!_alive[i]) continue;
            _meshes[i] = null;
            _deformers[i] = null;
            _renderData[i]?.Clear();
            _alive[i] = false;
        }

        _freeIndices.Clear();
        for (var i = _highWaterMark - 1; i >= 0; i--) _freeIndices.Push(i);
        Count = 0;
    }

    public bool IsValid(MeshHandle handle) =>
        handle.IsValid &&
        (uint)handle.Index < (uint)_highWaterMark &&
        _alive[handle.Index] &&
        _generations[handle.Index] == handle.Generation;

    public bool TryGetMesh(MeshHandle handle, out Mesh2D mesh)
    {
        if (IsValid(handle))
        {
            mesh = _meshes[handle.Index]!;
            return true;
        }

        mesh = null!;
        return false;
    }

    public bool TryGetDeformer(MeshHandle handle, out IMeshDeformer2D? deformer)
    {
        if (IsValid(handle))
        {
            deformer = _deformers[handle.Index];
            return true;
        }

        deformer = null;
        return false;
    }

    public void SetDeformer(MeshHandle handle, IMeshDeformer2D? deformer)
    {
        RequireValid(handle);
        _deformers[handle.Index] = deformer;
        _forceDirty[handle.Index] = true;
    }

    public void MarkDirty(MeshHandle handle)
    {
        RequireValid(handle);
        _forceDirty[handle.Index] = true;
    }

    public bool TryGetRenderData(MeshHandle handle, out MeshRenderData2D renderData)
    {
        if (IsValid(handle))
        {
            renderData = _renderData[handle.Index]!;
            return true;
        }

        renderData = null!;
        return false;
    }

    public void PrepareFrame(bool parallel = false)
    {
        if (Count == 0) return;

        if (parallel && Count > 256)
        {
            Parallel.For(0, _highWaterMark, index =>
            {
                if (_alive[index] && NeedsExtraction(index)) ExtractSlot(index);
            });
        }
        else
        {
            for (var index = 0; index < _highWaterMark; index++)
            {
                if (_alive[index] && NeedsExtraction(index)) ExtractSlot(index);
            }
        }
    }

    public IEnumerable<MeshBatch2D> GetBatches()
    {
        var groups = new Dictionary<Texture2D, List<MeshHandle>>();
        List<MeshHandle>? untextured = null;

        for (var index = 0; index < _highWaterMark; index++)
        {
            if (!_alive[index]) continue;

            var handle = new MeshHandle(index, _generations[index]);
            var texture = _meshes[index]!.Texture;

            if (texture is null)
            {
                (untextured ??= new List<MeshHandle>()).Add(handle);
                continue;
            }

            if (!groups.TryGetValue(texture, out var list))
            {
                list = new List<MeshHandle>();
                groups[texture] = list;
            }

            list.Add(handle);
        }

        foreach (var (texture, handles) in groups)
            yield return new MeshBatch2D(texture, handles);

        if (untextured is { Count: > 0 })
            yield return new MeshBatch2D(null, untextured);
    }

    public IEnumerable<MeshHandle> AliveHandles
    {
        get
        {
            for (var index = 0; index < _highWaterMark; index++)
                if (_alive[index])
                    yield return new MeshHandle(index, _generations[index]);
        }
    }

    private bool NeedsExtraction(int index) =>
        _forceDirty[index] ||
        _deformers[index] is not null ||
        _lastExtractedVersion[index] != _meshes[index]!.Version;

    private void ExtractSlot(int index)
    {
        var mesh = _meshes[index]!;
        var deformer = _deformers[index];
        var target = _renderData[index]!;

        MeshRenderExtractor.Extract(mesh, deformer, target);

        _lastExtractedVersion[index] = mesh.Version;
        _forceDirty[index] = false;
    }

    private int AllocateSlot()
    {
        if (_freeIndices.Count > 0) return _freeIndices.Pop();

        if (_highWaterMark == _meshes.Length) Grow();
        return _highWaterMark++;
    }

    private void Grow()
    {
        var newSize = Math.Max(_meshes.Length * 2, 16);
        Array.Resize(ref _meshes, newSize);
        Array.Resize(ref _deformers, newSize);
        Array.Resize(ref _renderData, newSize);
        Array.Resize(ref _generations, newSize);
        Array.Resize(ref _lastExtractedVersion, newSize);
        Array.Resize(ref _alive, newSize);
        Array.Resize(ref _forceDirty, newSize);
    }

    private void RequireValid(MeshHandle handle)
    {
        if (!IsValid(handle)) throw new ArgumentException("Invalid or stale mesh handle.", nameof(handle));
    }
}

