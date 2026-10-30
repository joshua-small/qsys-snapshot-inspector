using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Designer.Modules.SnapshotInspector;

/// <summary>Feature-specific hook contracts; runtime consent remains owned by Mods Core.</summary>
internal static class SnapshotStartup
{
    internal const string Designer1050 = "0e7150f8dc02e0159e019188780fb003ce025f5ec18c491609cf337d849bda60";
    internal const string Common1050 = "a6d1addda06bdcb1ad32b57c7480c947210c3d3ad287f5818c2ea64068bf5aee";
    internal const string Designer1051 = "05215790f1a08bb6bdb9db83a4b06ea631543f44f8cc6da04dc2b72864361fe9";
    internal const string Common1051 = "2413609b3c33a30d83506de1473aeea7057da7486133ec38517394d32d028ff1";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    internal static bool Allows(bool coreEnabled, string designer, string common) => coreEnabled &&
        ((designer == Designer1050 && common == Common1050) || (designer == Designer1051 && common == Common1051));

    internal static bool HasExpectedLocation(string directory, string designerPath, string commonPath) =>
        string.Equals(Path.GetFullPath(designerPath), Path.GetFullPath(Path.Combine(directory, "Q-Sys Designer.dll")), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Path.GetFullPath(commonPath), Path.GetFullPath(Path.Combine(directory, "ModuleCommon.dll")), StringComparison.OrdinalIgnoreCase);

    internal static void RequireBuild(bool coreEnabled, string directory, string designerPath, string commonPath)
    {
        if (!coreEnabled || !HasExpectedLocation(directory, designerPath, commonPath) ||
            !Allows(coreEnabled, Hash(designerPath), Hash(commonPath)))
            throw new InvalidOperationException("Snapshot Inspector requires an inspected Designer build enabled by Mods Core.");
    }

    internal sealed record Targets(MethodInfo Draw, ConstructorInfo Sidebar, MethodInfo[] Hover)
    {
        internal IEnumerable<MethodBase> All => Hover.Cast<MethodBase>().Append(Draw).Append(Sidebar);
    }

    // Resolve and validate the complete set before the caller installs any patches.
    internal static Targets Resolve(Type control, Type sidebar, Type drawingContext, IEnumerable<Type> types)
    {
        var draw = control.GetMethod("DrawIsNamed", Flags)
            ?? throw new InvalidOperationException("Snapshot drawing hook is unavailable.");
        RequireMethod(draw, typeof(void), drawingContext);
        var constructor = sidebar.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException("Snapshot sidebar constructor is unavailable.");
        if (sidebar.IsAbstract || sidebar.ContainsGenericParameters || constructor.IsStatic || constructor.GetMethodBody() is null)
            throw new InvalidOperationException("Snapshot sidebar constructor is unsupported.");
        var hover = new List<MethodInfo>();
        foreach (var type in types.Where(control.IsAssignableFrom))
        {
            var property = type.GetProperty("HoverText", Flags | BindingFlags.DeclaredOnly);
            if (property is null) continue;
            var getter = property.GetMethod
                ?? throw new InvalidOperationException("Snapshot hover hook is unreadable: " + type.FullName);
            RequireMethod(getter, typeof(string));
            hover.Add(getter);
        }
        if (hover.Count != 6 || hover.Distinct().Count() != hover.Count)
            throw new InvalidOperationException("Snapshot hover hook set is unsupported.");
        return new(draw, constructor, hover.ToArray());
    }

    private static void RequireMethod(MethodInfo method, Type returns, params Type[] parameters)
    {
        if (method.IsStatic || method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() is null || method.ReturnType != returns ||
            !method.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters))
            throw new InvalidOperationException("Snapshot hook signature is unsupported: " + method);
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
