using Designer.Modules.SnapshotInspector;
using System.Reflection;
using System.Runtime.CompilerServices;

int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
void Reject(Action action, string name, string? reason = null)
{
    try { action(); }
    catch (Exception ex) when (ex is InvalidOperationException or AmbiguousMatchException or IOException)
    {
        if (reason is not null && !ex.Message.Contains(reason, StringComparison.Ordinal)) throw new Exception(name + " rejected for the wrong reason", ex);
        checks++; return;
    }
    throw new Exception("Accepted unsupported contract: " + name);
}
var hosts = new[] { SnapshotStartup.Designer1050, SnapshotStartup.Designer1051, "unknown", "" };
var commons = new[] { SnapshotStartup.Common1050, SnapshotStartup.Common1051, "unknown", "" };
foreach (bool enabled in new[] { false, true })
    for (int h = 0; h < hosts.Length; h++)
        for (int c = 0; c < commons.Length; c++)
            Check(SnapshotStartup.Allows(enabled, hosts[h], commons[c]) == (enabled && h == c && h < 2), $"build gate {enabled}/{h}/{c}");
var directory = Path.Combine(Path.GetTempPath(), "snapshot-startup-location-fixture");
var hostPath = Path.Combine(directory, "Q-Sys Designer.dll");
var commonPath = Path.Combine(directory, "ModuleCommon.dll");
Check(SnapshotStartup.HasExpectedLocation(directory, hostPath, commonPath), "Loaded pair uses Core's installation directory");
Check(!SnapshotStartup.HasExpectedLocation(directory, commonPath, hostPath), "Swapped assembly locations rejected");
Check(!SnapshotStartup.HasExpectedLocation(directory, Path.Combine(directory, "other", "Q-Sys Designer.dll"), commonPath), "External Designer assembly rejected");
Check(!SnapshotStartup.HasExpectedLocation(directory, hostPath, Path.Combine(directory, "other", "ModuleCommon.dll")), "External common assembly rejected");
Reject(() => SnapshotStartup.RequireBuild(false, directory, "not-a-file", "not-a-file"), "Core denial before file access");
Reject(() => SnapshotStartup.RequireBuild(true, directory, hostPath, commonPath), "missing host files");

Type[] types = [typeof(Control), typeof(A), typeof(B), typeof(C), typeof(D), typeof(E)];
SnapshotStartup.Targets Resolve(Type? control = null, Type? sidebar = null, Type[]? set = null) =>
    SnapshotStartup.Resolve(control ?? typeof(Control), sidebar ?? typeof(Sidebar), typeof(Context), set ?? types);
var targets = Resolve();
Check(targets.Hover.Length == 6 && targets.All.Count() == 8, "Complete hook set");
Check(targets.Draw == typeof(Control).GetMethod(nameof(Control.DrawIsNamed)), "Drawing target identity");
Check(targets.Sidebar.DeclaringType == typeof(Sidebar), "Sidebar target identity");
Check(targets.Hover.Select(m => m.DeclaringType).ToHashSet().SetEquals(types), "Every declared hover target retained");
Check(Resolve(set: [.. types, typeof(Unrelated)]).Hover.Length == 6, "Unrelated type excluded");
Reject(() => Resolve(set: types[..5]), "missing hover target");
Reject(() => Resolve(set: [.. types, typeof(E)]), "duplicate hover target");
Reject(() => Resolve(set: [.. types[..5], typeof(WrongHover)]), "non-string hover");
Reject(() => Resolve(set: [.. types[..5], typeof(IndexedHover)]), "indexed hover");
Reject(() => Resolve(set: [.. types[..5], typeof(StaticHover)]), "static hover");
Reject(() => Resolve(set: [.. types[..5], typeof(AbstractHover)]), "abstract hover");
Reject(() => Resolve(set: [.. types[..5], typeof(GenericHover<>)]), "open generic hover");
Reject(() => Resolve(set: [.. types[..5], typeof(WriteOnlyHover)]), "write-only hover");
Reject(() => Resolve(sidebar: typeof(PrivateSidebar)), "private constructor");
Reject(() => Resolve(sidebar: typeof(ParameterizedSidebar)), "parameterized constructor");
Reject(() => Resolve(sidebar: typeof(AbstractSidebar)), "abstract sidebar");
Reject(() => Resolve(sidebar: typeof(GenericSidebar<>)), "open generic sidebar");
Reject(() => Resolve(control: typeof(NoDraw)), "missing drawing hook");
Reject(() => Resolve(control: typeof(StaticDraw)), "static drawing hook", "signature");
Reject(() => Resolve(control: typeof(WrongReturnDraw)), "drawing return type", "signature");
Reject(() => Resolve(control: typeof(WrongParameterDraw)), "drawing parameter type", "signature");
Reject(() => Resolve(control: typeof(AmbiguousDraw)), "ambiguous drawing hook");
Reject(() => Resolve(control: typeof(AbstractDraw)), "abstract drawing hook", "signature");
Reject(() => Resolve(control: typeof(RuntimeDraw)), "drawing hook without managed body", "signature");
Check(Sidebar.Created == 0 && Control.Executed == 0, "Preflight never invokes targets");
Console.WriteLine($"{checks} Snapshot startup checks passed.");

class Context { }
class Control
{
    public static int Executed;
    public void DrawIsNamed(Context context) => Executed++;
    public virtual string HoverText => "control";
}
class A : Control { public override string HoverText => "a"; }
class B : Control { public override string HoverText => "b"; }
class C : Control { public override string HoverText => "c"; }
class D : Control { public override string HoverText => "d"; }
class E : Control { public override string HoverText => "e"; }
class Unrelated { public string HoverText => "unrelated"; }
class WrongHover : Control { public new int HoverText => 1; }
class IndexedHover : Control { [IndexerName("HoverText")] public string this[int index] => "indexed"; }
class StaticHover : Control { public new static string HoverText => "static"; }
abstract class AbstractHover : Control { public abstract override string HoverText { get; } }
class GenericHover<T> : Control { public override string HoverText => "generic"; }
class WriteOnlyHover : Control { public new string HoverText { set { } } }
class Sidebar { public static int Created; public Sidebar() => Created++; }
class PrivateSidebar { private PrivateSidebar() { } }
class ParameterizedSidebar { public ParameterizedSidebar(int value) { } }
abstract class AbstractSidebar { public AbstractSidebar() { } }
class GenericSidebar<T> { public GenericSidebar() { } }
class NoDraw { }
class StaticDraw { public static void DrawIsNamed(Context context) { } }
class WrongReturnDraw { public int DrawIsNamed(Context context) => 1; }
class WrongParameterDraw { public void DrawIsNamed(int context) { } }
class AmbiguousDraw { public void DrawIsNamed(Context context) { } public void DrawIsNamed(int context) { } }
abstract class AbstractDraw { public abstract void DrawIsNamed(Context context); }
class RuntimeDraw { [MethodImpl(MethodImplOptions.InternalCall)] public extern void DrawIsNamed(Context context); }
