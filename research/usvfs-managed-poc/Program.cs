using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

if (args.Length > 0 && args[0] == "--child")
{
    return RunChild(args);
}

return await RunHostAsync(args);

static async Task<int> RunHostAsync(string[] args)
{
    if (args.Length != 1)
    {
        Console.Error.WriteLine("Usage: StalkerUsvfsManagedPoc <built-usvfs-source-root>");
        return 10;
    }

    var usvfsRoot = Path.GetFullPath(args[0]);
    CopyUsvfsRuntime(usvfsRoot, AppContext.BaseDirectory);

    var root = Path.Combine(Path.GetTempPath(), $"stalker-usvfs-managed-poc-{Environment.ProcessId}");
    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }

    var baseRoot = Path.Combine(root, "base");
    var modRoot = Path.Combine(root, "mod");
    var virtualRoot = Path.Combine(root, "virtual-root");
    var resultPath = Path.Combine(root, "result.txt");

    Directory.CreateDirectory(virtualRoot);
    WriteText(Path.Combine(baseRoot, "shared.txt"), "base");
    WriteText(Path.Combine(baseRoot, "base-only.txt"), "base");
    WriteText(Path.Combine(baseRoot, "gamedata", "config", "system.ltx"), "base-system");
    WriteText(Path.Combine(modRoot, "shared.txt"), "mod");
    WriteText(Path.Combine(modRoot, "mod-only.txt"), "mod");
    WriteText(Path.Combine(modRoot, "gamedata", "config", "system.ltx"), "mod-system");

    var plan = new UsvfsMappingPlan(
        virtualRoot,
        Path.Combine(root, "overwrite"),
        [
            new UsvfsMappingOperation(UsvfsMappingKind.DirectoryStatic, baseRoot, virtualRoot, "base", 1),
            new UsvfsMappingOperation(UsvfsMappingKind.DirectoryStatic, modRoot, virtualRoot, "mod", 2)
        ]);

    var runtime = new UsvfsRuntime(new OfficialUsvfsNativeApi());
    var result = await runtime.RunAsync(
        plan,
        new UsvfsProcessLaunchRequest(
            Environment.ProcessPath ?? throw new InvalidOperationException("Process path is unavailable."),
            $"--child {Quote(virtualRoot)} {Quote(resultPath)}",
            AppContext.BaseDirectory),
        new UsvfsRuntimeOptions("stalker_launcher_managed_usvfs_poc"));

    var output = File.Exists(resultPath) ? File.ReadAllText(resultPath) : string.Empty;
    Console.Write(output);

    var success = result.ExitCode == 0
                  && output.Contains("shared=mod", StringComparison.Ordinal)
                  && output.Contains("base-only=base", StringComparison.Ordinal)
                  && output.Contains("mod-only=mod", StringComparison.Ordinal)
                  && output.Contains("nested=mod-system", StringComparison.Ordinal);

    if (!success)
    {
        Console.Error.WriteLine($"Managed USVFS PoC failed. ExitCode={result.ExitCode}, ProcessId={result.ProcessId}");
        Console.Error.WriteLine($"PoC files: {root}");
        return 20;
    }

    Console.WriteLine($"Managed USVFS PoC passed. Files: {root}");
    return 0;
}

static int RunChild(string[] args)
{
    if (args.Length != 3)
    {
        Console.Error.WriteLine("Usage: StalkerUsvfsManagedPoc --child <virtual-root> <result-file>");
        return 30;
    }

    var virtualRoot = args[1];
    var resultPath = args[2];
    Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);

    File.WriteAllText(
        resultPath,
        string.Join(
            Environment.NewLine,
            [
                "shared=" + ReadText(Path.Combine(virtualRoot, "shared.txt")),
                "base-only=" + ReadText(Path.Combine(virtualRoot, "base-only.txt")),
                "mod-only=" + ReadText(Path.Combine(virtualRoot, "mod-only.txt")),
                "nested=" + ReadText(Path.Combine(virtualRoot, "gamedata", "config", "system.ltx"))
            ]) + Environment.NewLine);

    return 0;
}

static void CopyUsvfsRuntime(string usvfsRoot, string outputDirectory)
{
    CopyRequired(Path.Combine(usvfsRoot, "lib", "usvfs_x64.dll"), Path.Combine(outputDirectory, "usvfs_x64.dll"));
    CopyRequired(Path.Combine(usvfsRoot, "bin", "usvfs_proxy_x64.exe"), Path.Combine(outputDirectory, "usvfs_proxy_x64.exe"));
}

static void CopyRequired(string source, string destination)
{
    if (!File.Exists(source))
    {
        throw new FileNotFoundException("Required USVFS runtime file was not found.", source);
    }

    File.Copy(source, destination, overwrite: true);
}

static void WriteText(string path, string text)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, text);
}

static string ReadText(string path)
{
    return File.Exists(path) ? File.ReadAllText(path) : "<missing>";
}

static string Quote(string value)
{
    return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
