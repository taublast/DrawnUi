using System.Text.RegularExpressions;

namespace DrawnUi.Infrastructure;

public static class SkSl
{

    public static async Task<string> LoadFromResourcesAsync(string fileName)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        return json;
    }

    public static string LoadFromResources(string fileName)
    {
        using var stream = FileSystem.OpenAppPackageFileAsync(fileName).GetAwaiter().GetResult();
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return json;
    }

    public static Dictionary<string, SKRuntimeEffect> CompiledCache = new();

    /// <summary>
    /// Will compile your SKSL shader code into SKRuntimeEffect.
    /// </summary>
    /// <param name="shaderCode"></param>
    /// <returns></returns>
    public static SKRuntimeEffect Compile(string shaderCode)
    {
        return Compile(shaderCode, null, false);
    }

    /// <summary>
    /// Will compile your SKSL shader code into SKRuntimeEffect.
    /// The filename parameter is used for debugging and caching. Do not forget to disable caching if you edit/change shader code at runtime.
    /// </summary>
    /// <param name="shaderCode"></param>
    /// <param name="filename"></param>
    /// <returns></returns>
    public static SKRuntimeEffect Compile(string shaderCode, string filename, bool useCache = true)
    {
        SKRuntimeEffect compiled = null;

        if (useCache)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new Exception("SKSL needs a filename to be able to cache shader code.");
            }

            if (CompiledCache.TryGetValue(filename, out compiled))
            {
                Debug.WriteLine($"[SKSL] Using cached shader {filename}!");

                return compiled;
            }
        }

        string errors;
        compiled = SKRuntimeEffect.CreateShader(shaderCode, out errors);
        if (!string.IsNullOrEmpty(errors))
        {
            ThrowCompilationError(shaderCode, errors);
        }

        Debug.WriteLine($"[SKSL] Compiled shader {filename}!");

        if (useCache)
        {
            CompiledCache[filename] = compiled;
        }

        return compiled;
    }

    static void ThrowCompilationError(string shaderCode, string errors, string filename = null)
    {
        // Regular expression to find the line number in the error message
        var regex = new Regex(@"error: (\d+):");
        var match = regex.Match(errors);

        var error = string.Empty;
        if (match.Success && int.TryParse(match.Groups[1].Value, out int lineNumber))
        {
            var lines = shaderCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (lineNumber - 1 < lines.Length)
            {
                error = ($"Error occurred at line {lineNumber}: {lines[lineNumber - 1]}");
            }
            else
            {
                error = ($"Error line number {lineNumber} exceeds shader code line count.");
            }
        }
        else
        {
            error = errors;
        }
        if (!string.IsNullOrEmpty(error))
        {
            if (!string.IsNullOrEmpty(filename))
            {
                throw new ApplicationException($"Shader compilation of '{filename}' failed:{Environment.NewLine}{errors}{Environment.NewLine}" + error);
            }

            throw new ApplicationException($"Shader compilation failed:{Environment.NewLine}{errors}{Environment.NewLine}" + error);
        }
    }

 
    public static SKShader CreateShader(SKRuntimeEffect compiled, SKRuntimeEffectUniforms uniforms, Dictionary<string, SKShader> textures)
    {

        var children = new SKRuntimeEffectChildren(compiled);
        foreach (var texture in textures)
        {
            children.Add(texture.Key, texture.Value);
        }

        return compiled.ToShader(uniforms, children); ;
    }
 


}
