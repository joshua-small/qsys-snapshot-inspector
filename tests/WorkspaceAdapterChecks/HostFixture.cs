// Portable contract fixture, not the Designer runtime. Native persistence/rendering need separate acceptance.
using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace System.Windows
{
    public readonly record struct Point(double X, double Y);
    public readonly record struct Size(double Width, double Height);
    public enum HorizontalAlignment { Left, Center, Right }
    public sealed class Dispatcher { public void VerifyAccess() { } }
    public sealed class Application { public static Application Current { get; } = new(); public Dispatcher Dispatcher { get; } = new(); }
}
namespace System.Windows.Media
{
    public readonly record struct Color(byte A, byte R, byte G, byte B);
    public static class Colors
    {
        public static Color Black => new(255, 0, 0, 0);
        public static Color White => new(255, 255, 255, 255);
        public static Color Transparent => new(0, 0, 0, 0);
    }
}
namespace QsysMods.Core
{
    public static class Host
    {
        public static bool Enabled { get; set; } = true;
        public static List<string> Receipts { get; } = [];
        public static void RequiredReceipt(string product, string kind, object detail) => Receipts.Add(kind);
    }
}
namespace QSC.QSys
{
    public static class App
    {
        public static Design.Manager Manager { get; set; } = new();
        public static FixtureWindow Window { get; } = new();
    }
    public sealed class FixtureWindow { public void OpenPage(object page) { } }
}
namespace QSC.QSys.Tools { public static class Set { public static bool IsEditToolActive { get; set; } = true; } }
namespace QSC.QSys.Design
{
    public class Element
    {
        public static Predicate<Element>? AttachFailure { get; set; }
        public bool IsAttached { get; set; }
        public bool IsLocked { get; set; }
        public void Attach() => Attach(true);
        public virtual void Attach(bool value)
        {
            if (value && AttachFailure?.Invoke(this) is true) throw new InvalidOperationException("Injected native attachment failure");
            IsAttached = value;
        }
    }
    public class Object : Element
    {
        public Point Position { get; set; }
        public Size Size { get; set; } = new(100, 40);
        public string Label { get; set; } = "";
        public string CssClassName { get; set; } = "";
        public string LocalStyleString { get; set; } = "";
        public Color Color { get; set; }
        public bool IsHidden { get; set; }
        public Layer? Layer { get; set; }
    }
    public class Canvas : Element
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "Schematic";
        public uint PageBackgroundColor { get; set; }
        public List<Layer> Layers { get; }
        public IEnumerable<Object> AllChildren => Layers.SelectMany(l => l.Children);
        public Canvas() { Layers = [new Layer(this)]; }
        public override void Attach(bool value) { base.Attach(value); foreach (var layer in Layers) layer.Attach(value); }
    }
    public sealed class Layer(Canvas canvas) : Element
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsCurrentLayer { get; set; }
        public List<Object> Children { get; } = [];
        public void AddChild(Object value)
        {
            if (canvas.IsAttached) App.Manager.Record(() => { Children.Remove(value); value.Attach(false); value.Layer = null; });
            // Layer insertion and element attachment are separate native operations.
            Children.Add(value); value.Layer = this;
        }
        public void RemoveChild(Object value)
        {
            int index = Children.IndexOf(value);
            if (canvas.IsAttached) App.Manager.Record(() => { Children.Insert(index, value); value.Layer = this; value.Attach(true); });
            Children.Remove(value); value.Attach(false); value.Layer = null;
        }
        public override void Attach(bool value) { base.Attach(value); foreach (var child in Children) child.Attach(value); }
    }
    public sealed class PageList : IEnumerable
    {
        private readonly List<Canvas> items = [];
        public void Add(Canvas page) { App.Manager.Record(() => { items.Remove(page); page.Attach(false); }); items.Add(page); page.Attach(true); }
        public IEnumerator GetEnumerator() => items.GetEnumerator();
    }
    public sealed class Document { public PageList Pages { get; } = new(); }
    public sealed class Operation { public List<Action> Undo { get; } = []; public int Count => Undo.Count; }
    public sealed class Manager
    {
        public Document Document { get; } = new();
        public Operation _CurrentOp = new();
        public bool IsLoading => false;
        public bool IsInUndo => false;
        public bool IsInEndOperation => false;
        public int Starts, Commits, Cancels;
        public bool Active;
        public Action? OnStart;
        public void Record(Action action) { if (Active) _CurrentOp.Undo.Add(action); }
        public Guid StartOperation() { if (Active) return Guid.Empty; Starts++; Active = true; OnStart?.Invoke(); return Guid.NewGuid(); }
        public void EndOperation(Guid operation) { if (_CurrentOp.Count > 0) Commits++; _CurrentOp = new(); Active = false; }
        public void CancelOperation() { Cancels++; foreach (var undo in _CurrentOp.Undo.AsEnumerable().Reverse()) undo(); _CurrentOp = new(); }
    }
    public sealed record ValueBinding(string Id, string Name);
    public sealed class ControlStyle : Object
    {
        public required ValueBinding Value { get; init; }
        public bool IsInControlPanel { get; set; }
    }
    public sealed class Component : Element { public string Id { get; } = Guid.NewGuid().ToString(); }
    public sealed record Descriptor(string ControlId, string VisualStyle = "Fader", bool IsPresetable = true, string SnapshotProxy = "", double MinValue = 0, double MaxValue = 1);
    public static class ComponentHelper
    {
        public static int Calls, FailAt;
        public static ControlStyle CreateControlStyle(Component component, Descriptor descriptor, bool panel)
        {
            if (++Calls == FailAt) throw new InvalidOperationException("Injected native factory failure");
            return new() { Value = new(component.Id, descriptor.ControlId), CssClassName = "owner-source-style" };
        }
    }
}
namespace QSC.QSys.Design.Graphics
{
    public sealed class TextBlock : Object
    {
        public Color Fill { get; set; }
        public double FontSize { get; set; }
        public HorizontalAlignment HorizontalTextAlignment { get; set; }
    }
    public sealed class PopupPage : Canvas { public Color Color { get; set; } = new(255, 0, 255, 255); }
    public sealed class Popup : Object
    {
        public PopupPage Content { get; } = new();
        public double PopupWidth1 { get; set; } = 50;
        public double PopupWidth2 { get; set; } = 50;
        public double PopupHeight1 { get; set; } = 50;
        public double PopupHeight2 { get; set; } = 50;
        public double PopupMargin { get; set; } = 3;
        public Size ContentSize => new(PopupWidth1 + PopupWidth2 - 2 * PopupMargin, PopupHeight1 + PopupHeight2 - 2 * PopupMargin);
        public override void Attach(bool value) { base.Attach(value); Content.Attach(value); }
    }
}
namespace Designer.Modules.SnapshotInspector
{
    internal static class SnapshotHost
    {
        internal const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static Type Type(string name) => typeof(SnapshotHost).Assembly.GetType("QSC.QSys." + name, true)!;
        internal static object? Get(object? owner, string name)
        {
            if (owner is null) return null;
            var type = owner as Type ?? owner.GetType();
            return type.GetProperty(name, Flags)?.GetValue(owner is Type ? null : owner) ?? type.GetField(name, Flags)?.GetValue(owner is Type ? null : owner);
        }
        internal static object? Call(object owner, string name, params object?[] args)
        {
            var type = owner as Type ?? owner.GetType();
            return type.GetMethods(Flags).Single(m => m.Name == name && !m.IsGenericMethod && m.GetParameters().Length == args.Length
                && m.GetParameters().Select((p, i) => args[i] is null || p.ParameterType.IsInstanceOfType(args[i])).All(b => b)).Invoke(owner is Type ? null : owner, args);
        }
        internal static object[] Items(object? value) => value is IEnumerable list ? list.Cast<object>().ToArray() : [];
        internal static string Text(object? owner, string name) => Get(owner, name)?.ToString() ?? "";
        internal static void Set(object owner, string name, object value)
        {
            var property = owner.GetType().GetProperty(name, Flags) ?? throw new MissingMemberException(owner.GetType().Name, name);
            var previous = property.GetValue(owner);
            if (owner is QSC.QSys.Design.Element { IsAttached: true }) QSC.QSys.App.Manager.Record(() => property.SetValue(owner, previous));
            property.SetValue(owner, value);
        }
        internal static object Manager => QSC.QSys.App.Manager;
        internal static object Document => QSC.QSys.App.Manager.Document;
    }
    internal sealed record SnapshotKey(object Id, string Control);
    internal sealed record SnapshotRow(object BankObject, object ComponentObject, object? Descriptor, SnapshotKey Key,
        string Bank, string Component, string Path, string Control, string ControlId, string IncludedBy, string SnapshotId);
}
