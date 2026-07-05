using System.Reflection;

namespace Momentum.SharedKernel.Modules;

/// <summary>
/// Discovers module assemblies by naming convention instead of a hand-maintained
/// list, so that "drop a folder implementing IModule" (+ a ProjectReference from
/// Momentum.Api so the DLL is copied to the output folder) is genuinely all it
/// takes to register a module — no edits to Program.cs or the DbContext.
///
/// Used from two places that must agree on the same assembly set:
///   - Momentum.Api's Program.cs, to build the list of IModule instances.
///   - Momentum.Infrastructure's AppDbContext.OnModelCreating, to call
///     ApplyConfigurationsFromAssembly for every module's EF entity configs.
///
/// Both run with AppContext.BaseDirectory pointing at Momentum.Api's build output
/// (true at runtime, and true at `dotnet ef` design-time too when invoked with
/// --startup-project src/Momentum.Api), so scanning that folder is reliable in
/// both cases.
/// </summary>
public static class ModuleAssemblyScanner
{
    private const string ModuleAssemblyPrefix = "Momentum.Modules.";

    private static IReadOnlyList<Assembly>? _cache;

    /// <summary>
    /// Every module assembly found in the app's base directory, loaded and cached
    /// for the lifetime of the process.
    /// </summary>
    public static IReadOnlyList<Assembly> GetModuleAssemblies()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        var baseDirectory = AppContext.BaseDirectory;

        var dllPaths = Directory.Exists(baseDirectory)
            ? Directory.GetFiles(baseDirectory, $"{ModuleAssemblyPrefix}*.dll", SearchOption.TopDirectoryOnly)
            : [];

        var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.GetName().Name is not null)
            .ToDictionary(a => a.GetName().Name!, StringComparer.Ordinal);

        var assemblies = new List<Assembly>();

        foreach (var path in dllPaths.OrderBy(p => p, StringComparer.Ordinal))
        {
            var assemblyName = Path.GetFileNameWithoutExtension(path);

            if (alreadyLoaded.TryGetValue(assemblyName, out var loaded))
            {
                assemblies.Add(loaded);
                continue;
            }

            try
            {
                assemblies.Add(Assembly.LoadFrom(path));
            }
            catch (BadImageFormatException)
            {
                // Not a managed assembly (e.g. a native dependency that happens to
                // match the glob) — ignore.
            }
        }

        _cache = assemblies;
        return _cache;
    }

    /// <summary>
    /// Instantiates every non-abstract <see cref="IModule"/> found across the
    /// discovered module assemblies, optionally filtered by the
    /// <c>Modules__Enabled</c> allow-list (comma-separated module names; empty/unset
    /// means "all discovered modules are enabled").
    /// </summary>
    public static IReadOnlyList<IModule> DiscoverModules(IReadOnlyCollection<string>? enabledModuleNames = null)
    {
        var moduleTypes = GetModuleAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IModule).IsAssignableFrom(t))
            .ToList();

        var modules = moduleTypes
            .Select(t => (IModule)Activator.CreateInstance(t)!)
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToList();

        if (enabledModuleNames is { Count: > 0 })
        {
            var allowList = new HashSet<string>(enabledModuleNames, StringComparer.OrdinalIgnoreCase);
            modules = modules.Where(m => allowList.Contains(m.Name)).ToList();
        }

        return modules;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
