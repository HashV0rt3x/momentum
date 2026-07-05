using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Momentum.SharedKernel.Modules;

/// <summary>
/// Every module (Auth, Settings, Habits, Tasks, ...) implements exactly one of
/// these, typically in a "&lt;ModuleName&gt;Module.cs" file at the root of its
/// project. Program.cs never references a module type by name — modules are
/// discovered by <see cref="ModuleAssemblyScanner"/> from the built output
/// directory, so adding a new module means: create the project, add a
/// ProjectReference from Momentum.Api (so its DLL lands in the output folder),
/// implement IModule. No other file needs to change.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Stable machine name used for Modules__Enabled filtering and logging.
    /// Convention: the module's folder name, e.g. "Habits", "Platform.Auth".
    /// </summary>
    string Name { get; }

    /// <summary>Register the module's own services (DI). Called for every discovered module.</summary>
    void AddServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Map the module's endpoints. Called for every discovered <em>enabled</em> module.</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
