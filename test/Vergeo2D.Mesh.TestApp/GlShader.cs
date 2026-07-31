using Silk.NET.OpenGL;

internal static class GlShader
{
    public static uint CreateProgram(GL gl, string vertexSource, string fragmentSource)
    {
        var vertexShader = Compile(gl, ShaderType.VertexShader, vertexSource);
        var fragmentShader = Compile(gl, ShaderType.FragmentShader, fragmentSource);

        var program = gl.CreateProgram();
        gl.AttachShader(program, vertexShader);
        gl.AttachShader(program, fragmentShader);
        gl.BindFragDataLocation(program, 0, "FragColor");
        gl.LinkProgram(program);

        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var status);
        if (status == 0) throw new InvalidOperationException(gl.GetProgramInfoLog(program));

        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);
        return program;
    }

    private static uint Compile(GL gl, ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);

        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status == 0) throw new InvalidOperationException(gl.GetShaderInfoLog(shader));

        return shader;
    }
}
