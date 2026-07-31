using Silk.NET.Maths;
using Silk.NET.Windowing;

var backend = MeshTestBackend.OpenGL;
var runAllBackends = false;
var smokeOnly = false;
var imagePath = Path.Combine(AppContext.BaseDirectory, "assets", "character-base.png");

for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];
    if (arg is "--backend" or "-b")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine("Missing backend after --backend. Use opengl, vulkan, or dx.");
            return 1;
        }

        var value = args[++i];
        if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            runAllBackends = true;
            continue;
        }

        if (!TryParseBackend(value, out backend))
        {
            Console.Error.WriteLine($"Unknown backend: {value}");
            Console.Error.WriteLine("Use opengl, vulkan, dx, or all.");
            return 1;
        }

        continue;
    }

    if (arg.StartsWith("--backend=", StringComparison.OrdinalIgnoreCase))
    {
        var value = arg["--backend=".Length..];
        if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            runAllBackends = true;
            continue;
        }

        if (!TryParseBackend(value, out backend))
        {
            Console.Error.WriteLine($"Unknown backend: {value}");
            Console.Error.WriteLine("Use opengl, vulkan, dx, or all.");
            return 1;
        }

        continue;
    }

    if (arg is "--smoke")
    {
        smokeOnly = true;
        continue;
    }

    if (arg is "--help" or "-h")
    {
        PrintUsage();
        return 0;
    }

    imagePath = arg;
}

if (!File.Exists(imagePath))
{
    Console.Error.WriteLine($"Test image not found: {imagePath}");
    return 1;
}

if (runAllBackends && !smokeOnly)
{
    Console.Error.WriteLine("--backend all is only available with --smoke.");
    return 1;
}

if (!smokeOnly && backend != MeshTestBackend.OpenGL)
{
    Console.Error.WriteLine($"{MeshBackendSmokeTest.GetBackendLabel(backend)} interactive rendering is not implemented in this test app yet.");
    Console.Error.WriteLine("Use --smoke to validate the mesh/render-data pipeline, or run --backend opengl for the interactive preview.");
    return 1;
}

if (smokeOnly)
{
    var backends = runAllBackends
        ? new[] { MeshTestBackend.OpenGL, MeshTestBackend.Vulkan, MeshTestBackend.DirectX }
        : new[] { backend };

    foreach (var smokeBackend in backends)
    {
        MeshBackendSmokeTest.Run(imagePath, smokeBackend);
    }

    return 0;
}

var options = CreateWindowOptions(backend);
options.Title = $"Vergeo2D.Mesh Test Render ({MeshBackendSmokeTest.GetBackendLabel(backend)})";
options.Size = new Vector2D<int>(1280, 720);

using var app = new MeshTestWindow(options, imagePath);
app.Run();
return 0;

static WindowOptions CreateWindowOptions(MeshTestBackend backend)
{
    var options = backend == MeshTestBackend.Vulkan
        ? WindowOptions.DefaultVulkan
        : WindowOptions.Default;

    options.API = backend switch
    {
        MeshTestBackend.OpenGL => new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
        MeshTestBackend.Vulkan => GraphicsAPI.DefaultVulkan,
        MeshTestBackend.DirectX => GraphicsAPI.None,
        _ => options.API
    };

    return options;
}

static bool TryParseBackend(string value, out MeshTestBackend backend)
{
    switch (value.Trim().ToLowerInvariant())
    {
        case "opengl":
        case "gl":
            backend = MeshTestBackend.OpenGL;
            return true;
        case "vulkan":
        case "vk":
            backend = MeshTestBackend.Vulkan;
            return true;
        case "directx":
        case "direct3d":
        case "direct3d11":
        case "d3d11":
        case "dx":
            backend = MeshTestBackend.DirectX;
            return true;
        default:
            backend = default;
            return false;
    }
}

static void PrintUsage()
{
    Console.WriteLine("Vergeo2D.Mesh.TestApp");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- [imagePath] [--backend opengl|vulkan|dx]");
    Console.WriteLine("  dotnet run --project test/Vergeo2D.Mesh.TestApp/Vergeo2D.Mesh.TestApp.csproj -- --backend all --smoke");
}
