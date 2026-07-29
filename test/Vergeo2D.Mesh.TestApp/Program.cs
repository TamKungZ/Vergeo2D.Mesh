using Silk.NET.Maths;
using Silk.NET.Windowing;

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
