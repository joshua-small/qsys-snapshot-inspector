using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using QsysMods.Core;

namespace Designer.Modules.SnapshotInspector;

internal enum WorkspaceDestinationKind { NewPage, NewPopup, Update }
internal sealed record WorkspaceDestination(string Name, WorkspaceDestinationKind Kind, object? Page = null,
    object? Canvas = null, object? Popup = null)
{
    public override string ToString() => Name;
}

/// <summary>Exact-host native adapter. Model changes use a single cancellable Designer operation.</summary>
internal static class WorkspaceAdapter
{
    internal sealed record Preview(object Document, WorkspaceDestination Destination, SnapshotRow[] Rows,
        WorkspaceMember[] Members, WorkspacePlan Plan, string SceneStamp, string SourceStamp, object? Marker, string Title);

    internal static WorkspaceDestination[] Destinations()
    {
        var choices = new List<WorkspaceDestination> { new("New schematic page", WorkspaceDestinationKind.NewPage) };
        foreach (var page in SnapshotHost.Items(SnapshotHost.Get(SnapshotHost.Document, "Pages")))
        {
            string title = SnapshotHost.Text(page, "Title");
            choices.Add(new("New Popup on " + title, WorkspaceDestinationKind.NewPopup, page));
            AddExisting(page, page, null, title);
            foreach (var popup in Objects(page).Where(IsPopup))
                if (SnapshotHost.Get(popup, "Content") is { } content)
                    AddExisting(page, content, popup, title + " / " + SnapshotHost.Text(popup, "Label"));
        }
        return choices.ToArray();

        void AddExisting(object page, object canvas, object? popup, string title)
        {
            if (Objects(canvas).Any(IsMarker))
                choices.Add(new("Update " + title, WorkspaceDestinationKind.Update, page, canvas, popup));
        }
    }

    internal static Preview Prepare(WorkspaceDestination destination, SnapshotRow[] rows, string title, bool removeObsolete)
    {
        Guard();
        var document = SnapshotHost.Document ?? throw new InvalidOperationException("Open a design first.");
        CheckDestination(document, destination);
        var members = Members(rows);
        object? marker = null;
        WorkspaceManifest manifest;
        WorkspaceItem[] existing = [];
        if (destination.Kind == WorkspaceDestinationKind.Update)
        {
            var markers = Objects(destination.Canvas!).Where(IsMarker).ToArray();
            if (markers.Length != 1) throw new InvalidOperationException("Workspace ownership information is missing or ambiguous.");
            marker = markers[0];
            manifest = WorkspacePlanner.Decode(SnapshotHost.Text(marker, "Label"), SnapshotHost.Text(destination.Canvas, "Id"));
            existing = OwnedItems(destination.Canvas!).Select(x => x.Item).ToArray();
        }
        else
        {
            if (members.Length == 0) throw new InvalidOperationException("There are no controls in these results.");
            manifest = WorkspacePlanner.Empty("pending");
        }
        var plan = WorkspacePlanner.Plan(members, manifest, existing, removeObsolete);
        if (marker is not null) CheckUnlocked(marker);
        if (destination.Canvas is not null)
            foreach (var (widget, item) in OwnedItems(destination.Canvas).Where(x => plan.Removed.Any(m => m.Token == x.Item.Token)))
            {
                var ownerLayer = Layers(destination.Canvas).SingleOrDefault(l => SnapshotHost.Items(SnapshotHost.Get(l, "Children")).Any(c => ReferenceEquals(c, widget)))
                    ?? throw new InvalidOperationException("An obsolete control was grouped or moved. Keep obsolete controls or ungroup it before removal.");
                CheckUnlocked(ownerLayer);
            }
        return new(document, destination, rows.ToArray(), members, plan, Scene(destination), DescriptorStamp(rows), marker, title);
    }

    internal static string Apply(Preview preview, Func<SnapshotRow[]> readRows, CancellationToken cancellation = default)
    {
        Guard();
        Application.Current.Dispatcher.VerifyAccess();
        var destination = preview.Destination;
        if (!Current()) throw new InvalidOperationException("The source or destination changed. Refresh the preview.");
        if (!preview.Plan.Changed) return "The workspace already matches this preview. Nothing changed.";
        var manager = SnapshotHost.Manager!;
        var originalPages = SnapshotHost.Items(SnapshotHost.Get(preview.Document, "Pages"));
        var preservedCanvas = destination.Canvas ?? destination.Page;
        var removing = preview.Plan.Removed.Select(m => m.Token).ToHashSet(StringComparer.Ordinal);
        var preservedObjects = preservedCanvas is null ? [] : Objects(preservedCanvas).Where(o => !IsMarker(o)
            && !removing.Contains(WorkspacePlanner.ControlToken(SnapshotHost.Text(o, "CssClassName")) ?? ""))
            .Select(o => (Object: o, Stamp: ObjectStamp(o))).ToArray();
        var transaction = new NativeTransaction(manager);
        object? canvas = destination.Canvas, popup = destination.Popup, marker = preview.Marker;
        var created = new List<(object Widget, string Token, WorkspaceKey Key)>();
        var removed = new List<object>();
        WorkspaceManifest? finalManifest = null;
        Host.RequiredReceipt("snapshot-inspector", "workspace-start", new { destination = destination.Name,
            added = preview.Plan.Added.Select(m => m.Key), removed = preview.Plan.Removed.Select(m => m.Key),
            keptObsolete = preview.Plan.Retired.Select(m => m.Key) });
        WorkspaceTransaction.Apply(transaction, Current, () =>
        {
            if (destination.Kind == WorkspaceDestinationKind.NewPage)
            {
                canvas = Activator.CreateInstance(SnapshotHost.Type("Design.Canvas"))!;
                SnapshotHost.Set(canvas, "Title", UniqueTitle(preview.Document, "Snapshot controls - " + preview.Title));
                SnapshotHost.Set(canvas, "PageBackgroundColor", 0xff282b2eu);
            }
            else if (destination.Kind == WorkspaceDestinationKind.NewPopup)
            {
                popup = Activator.CreateInstance(SnapshotHost.Type("Design.Graphics.Popup"), nonPublic: true)!;
                canvas = SnapshotHost.Get(popup, "Content") ?? throw new InvalidOperationException("Popup content is unavailable.");
                SnapshotHost.Set(canvas, "Color", Color.FromRgb(40, 43, 46));
                SnapshotHost.Set(popup, "Label", "Snapshot controls - " + preview.Title);
                SnapshotHost.Set(popup, "Position", new Point(30, Bottom(destination.Page!) + 30));
                SnapshotHost.Set(popup, "Size", new Size(240, 40));
                SnapshotHost.Set(popup, "BackgroundColor", Color.FromRgb(40, 43, 46));
                SnapshotHost.Set(popup, "Color", Colors.White);
            }
            if (canvas is null) throw new InvalidOperationException("No workspace canvas is available.");
            var layer = LayerFor(canvas);
            CheckUnlocked(layer);
            double y = Bottom(canvas) + 30;
            var nextByKey = preview.Plan.Next.Controls.ToDictionary(m => m.Key);
            foreach (var member in preview.Plan.Added)
            {
                cancellation.ThrowIfCancellationRequested();
                var row = preview.Rows.First(r => Key(r) == member.Key);
                var widget = SnapshotHost.Call(SnapshotHost.Type("Design.ComponentHelper"), "CreateControlStyle",
                    row.ComponentObject, row.Descriptor, false) ?? throw new InvalidOperationException("No native widget for " + member.Caption);
                SnapshotHost.Set(widget, "IsInControlPanel", false);
                if (Binding(widget) != member.Key) throw new InvalidOperationException("The native widget returned an unexpected control binding.");
                var size = (Size)SnapshotHost.Get(widget, "Size")!;
                double nextY = WorkspacePlanner.NextRow(y, size.Width, size.Height);
                var owned = nextByKey[member.Key];
                string classes = SnapshotHost.Text(widget, "CssClassName");
                SnapshotHost.Set(widget, "CssClassName", (classes + " " + WorkspacePlanner.ControlPrefix + owned.Token).Trim());
                SnapshotHost.Set(widget, "Position", new Point(340, y));
                var label = Label(member.Caption + "\n" + string.Join(", ", member.Banks), 30, y, 290, nextY - y - 10);
                SnapshotHost.Call(layer, "AddChild", label);
                SnapshotHost.Call(layer, "AddChild", widget);
                created.Add((widget, owned.Token, member.Key));
                y = nextY;
            }
            var removeTokens = preview.Plan.Removed.Select(m => m.Token).ToHashSet(StringComparer.Ordinal);
            foreach (var (widget, item) in OwnedItems(canvas).Where(x => removeTokens.Contains(x.Item.Token)))
            {
                cancellation.ThrowIfCancellationRequested();
                // Removing an entire group could also delete owner annotations. Require a direct layer child.
                var ownerLayer = Layers(canvas).SingleOrDefault(l => SnapshotHost.Items(SnapshotHost.Get(l, "Children")).Any(c => ReferenceEquals(c, widget)))
                    ?? throw new InvalidOperationException("An obsolete control was grouped or moved. Keep obsolete controls or ungroup it before removal.");
                CheckUnlocked(ownerLayer); CheckUnlocked(widget);
                SnapshotHost.Call(ownerLayer, "RemoveChild", widget); removed.Add(widget);
            }
            finalManifest = preview.Plan.Next with { CanvasId = SnapshotHost.Text(canvas, "Id") };
            if (marker is null)
            {
                marker = Label("", 0, 0, 1, 1);
                SnapshotHost.Set(marker, "CssClassName", WorkspacePlanner.ManifestClass);
                SnapshotHost.Set(marker, "IsHidden", true);
                SnapshotHost.Set(marker, "Label", WorkspacePlanner.Encode(finalManifest));
                SnapshotHost.Call(layer, "AddChild", marker);
            }
            else SnapshotHost.Set(marker, "Label", WorkspacePlanner.Encode(finalManifest));
            if (destination.Kind == WorkspaceDestinationKind.NewPopup)
            {
                double right = Objects(canvas).Where(o => !IsMarker(o)).Select(o => Right(o)).DefaultIfEmpty(600).Max();
                double bottom = Bottom(canvas);
                SnapshotHost.Set(popup!, "PopupWidth1", 0.0); SnapshotHost.Set(popup!, "PopupWidth2", Math.Ceiling(right + 36));
                SnapshotHost.Set(popup!, "PopupHeight1", 0.0); SnapshotHost.Set(popup!, "PopupHeight2", Math.Ceiling(bottom + 36));
                var parentLayer = LayerFor(destination.Page!);
                CheckUnlocked(parentLayer); SnapshotHost.Call(parentLayer, "AddChild", popup);
            }
            else if (destination.Kind == WorkspaceDestinationKind.NewPage)
                SnapshotHost.Call(SnapshotHost.Get(preview.Document, "Pages")!, "Add", canvas);
            else if (popup is not null && created.Count > 0)
            {
                // Grow only when an appended widget needs space; preserve the owner's anchor and existing size.
                var contentSize = (Size)SnapshotHost.Get(popup, "ContentSize")!;
                double right = Objects(canvas).Where(o => !IsMarker(o)).Select(Right).DefaultIfEmpty(0).Max() + 30;
                double bottom = Bottom(canvas) + 30;
                if (right > contentSize.Width)
                    SnapshotHost.Set(popup, "PopupWidth2", (double)SnapshotHost.Get(popup, "PopupWidth2")! + Math.Ceiling(right - contentSize.Width));
                if (bottom > contentSize.Height)
                    SnapshotHost.Set(popup, "PopupHeight2", (double)SnapshotHost.Get(popup, "PopupHeight2")! + Math.Ceiling(bottom - contentSize.Height));
            }
        }, () =>
        {
            if (canvas is null || marker is null || finalManifest is null) throw new InvalidOperationException("Workspace creation did not complete.");
            var items = OwnedItems(canvas).ToArray();
            if (WorkspacePlanner.Plan(preview.Members, finalManifest, items.Select(x => x.Item), false).Changed)
                throw new InvalidOperationException("The resulting workspace does not match the preview.");
            foreach (var (widget, token, key) in created)
                if (SnapshotHost.Get(widget, "IsAttached") is not true || Binding(widget) != key
                    || WorkspacePlanner.ControlToken(SnapshotHost.Text(widget, "CssClassName")) != token)
                    throw new InvalidOperationException("A generated control did not attach with its intended binding.");
            if (removed.Any(o => SnapshotHost.Get(o, "IsAttached") is true))
                throw new InvalidOperationException("An obsolete control was not removed.");
            if (SnapshotHost.Text(marker, "Label") != WorkspacePlanner.Encode(finalManifest))
                throw new InvalidOperationException("Workspace ownership information was not stored.");
            if (preservedCanvas is not null)
            {
                var present = Objects(preservedCanvas).ToHashSet(ReferenceEqualityComparer.Instance);
                if (preservedObjects.Any(p => !present.Contains(p.Object) || ObjectStamp(p.Object) != p.Stamp))
                    throw new InvalidOperationException("An existing widget or annotation changed unexpectedly.");
            }
        }, cancellation, () =>
        {
            if (!SnapshotHost.Items(SnapshotHost.Get(preview.Document, "Pages")).SequenceEqual(originalPages, ReferenceEqualityComparer.Instance)
                || Scene(destination) != preview.SceneStamp)
                throw new InvalidOperationException("Cancellation did not restore the original destination. Inspect the document before continuing.");
        });
        // A navigation/logging problem after commit must not be reported as a failed or cancelled edit.
        var warnings = new List<string>();
        try { SnapshotHost.Call(SnapshotHost.Get(SnapshotHost.Type("App"), "Window")!, "OpenPage", destination.Page ?? canvas!); }
        catch (Exception ex) { warnings.Add("The edit completed, but the page could not be opened: " + ex.GetBaseException().Message); }
        try { Host.RequiredReceipt("snapshot-inspector", "workspace-completed", new { workspace = finalManifest!.WorkspaceId, added = created.Count, removed = removed.Count }); }
        catch { warnings.Add("The edit completed, but its completion log could not be saved."); }
        return $"Workspace updated: {created.Count} added, {removed.Count} removed, {preview.Plan.Retired.Length} obsolete controls kept. Undo reverses this update."
            + (warnings.Count == 0 ? "" : "\n" + string.Join("\n", warnings));

        bool Current()
        {
            if (!ReferenceEquals(preview.Document, SnapshotHost.Document)) return false;
            CheckDestination(preview.Document, destination);
            var rows = readRows();
            if (rows.Length != preview.Rows.Length || Scene(destination) != preview.SceneStamp || DescriptorStamp(rows) != preview.SourceStamp) return false;
            return rows.Zip(preview.Rows).All(pair => pair.First with { Descriptor = null } == pair.Second with { Descriptor = null }
                && ReferenceEquals(pair.First.ComponentObject, pair.Second.ComponentObject)
                && ReferenceEquals(pair.First.BankObject, pair.Second.BankObject));
        }
    }

    private static WorkspaceMember[] Members(SnapshotRow[] rows)
    {
        if (rows.Any(r => r.Descriptor is null || SnapshotHost.Get(r.ComponentObject, "IsAttached") is not true))
            throw new InvalidOperationException("A source control is unavailable. Refresh the results.");
        if (rows.Any(r => SnapshotHost.Text(r.Descriptor, "VisualStyle") == "None"))
            throw new InvalidOperationException("These results include controls with no native visual. Filter them out before generating a workspace.");
        return WorkspacePlanner.Normalize(rows.Select(r => new WorkspaceMember(Key(r),
            r.Path + " / " + r.Component + " / " + r.Control + " (" + r.ControlId + ")", [r.Bank])));
    }
    private static WorkspaceKey Key(SnapshotRow row) => new(row.Key.Id.ToString()!, row.ControlId);
    private static string DescriptorStamp(SnapshotRow[] rows) => JsonSerializer.Serialize(rows.Select(r =>
        new[] { "VisualStyle", "rc", "IsPresetable", "SnapshotProxy", "MinValue", "MaxValue" }
            .Select(p => SnapshotHost.Text(r.Descriptor, p)).ToArray()));
    private static WorkspaceKey Binding(object widget)
    {
        var value = SnapshotHost.Get(widget, "Value");
        return new(SnapshotHost.Text(value, "Id"), SnapshotHost.Text(value, "Name"));
    }
    private static object[] Layers(object canvas) => SnapshotHost.Items(SnapshotHost.Get(canvas, "Layers"));
    // Avoid the host's lazy CurrentLayer getter while inspecting a preview.
    private static object LayerFor(object canvas) => Layers(canvas).FirstOrDefault(l => SnapshotHost.Get(l, "IsCurrentLayer") is true)
        ?? Layers(canvas).FirstOrDefault() ?? throw new InvalidOperationException("The destination has no layers.");
    private static object[] Objects(object canvas) => SnapshotHost.Items(SnapshotHost.Get(canvas, "AllChildren"));
    private static bool IsMarker(object value) => value.GetType().FullName == "QSC.QSys.Design.Graphics.TextBlock"
        && SnapshotHost.Text(value, "CssClassName") == WorkspacePlanner.ManifestClass;
    private static bool IsPopup(object value) => value.GetType().FullName == "QSC.QSys.Design.Graphics.Popup";
    private static (object Widget, WorkspaceItem Item)[] OwnedItems(object canvas) => Objects(canvas)
        .Select(widget => (Widget: widget, Token: WorkspacePlanner.ControlToken(SnapshotHost.Text(widget, "CssClassName"))))
        .Where(x => x.Token is not null).Select(x => (x.Widget, new WorkspaceItem(x.Token!, Binding(x.Widget), SnapshotHost.Get(x.Widget, "IsLocked") is true))).ToArray();
    private static double Bottom(object canvas) => Objects(canvas).Where(o => !IsMarker(o)).Select(o =>
    {
        var position = SnapshotHost.Get(o, "Position"); var size = SnapshotHost.Get(o, "Size");
        if (position is not Point p || size is not Size s) throw new InvalidOperationException("An object has no supported layout bounds.");
        return WorkspacePlanner.ExistingBottom(p.X, p.Y, s.Width, s.Height);
    }).DefaultIfEmpty(0).Max();
    private static double Right(object item) => ((Point)SnapshotHost.Get(item, "Position")!).X + ((Size)SnapshotHost.Get(item, "Size")!).Width;
    private static string ObjectStamp(object item) => JsonSerializer.Serialize(new { binding = Binding(item),
        properties = new[] { "Position", "Size", "Label", "CssClassName", "LocalStyleString", "Color", "Fill", "BackgroundColor",
            "TextColor", "Font", "Face", "FontSize", "VisualStyle", "IsHidden", "IsLocked" }.Select(p => SnapshotHost.Text(item, p)).ToArray() });

    private static string Scene(WorkspaceDestination destination)
    {
        object? canvas = destination.Canvas ?? destination.Page;
        if (canvas is null) return "new-page";
        return JsonSerializer.Serialize(new { canvas = SnapshotHost.Text(canvas, "Id"), title = SnapshotHost.Text(canvas, "Title"),
            currentLayer = SnapshotHost.Text(LayerFor(canvas), "Id"),
            popup = destination.Popup is null ? null : new[] { "Position", "Size", "PopupWidth1", "PopupWidth2", "PopupHeight1", "PopupHeight2", "PopupMargin", "IsLocked" }.Select(p => SnapshotHost.Text(destination.Popup, p)).ToArray(),
            layers = Layers(canvas).Select(l => new { id = SnapshotHost.Text(l, "Id"), locked = SnapshotHost.Get(l, "IsLocked") }),
            objects = Objects(canvas).Select(o => new { type = o.GetType().FullName,
                position = SnapshotHost.Text(o, "Position"), size = SnapshotHost.Text(o, "Size"),
                label = SnapshotHost.Text(o, "Label"), classes = SnapshotHost.Text(o, "CssClassName"),
                locked = SnapshotHost.Get(o, "IsLocked"), hidden = SnapshotHost.Get(o, "IsHidden"), binding = Binding(o) }) });
    }
    private static string UniqueTitle(object document, string title)
    {
        var names = SnapshotHost.Items(SnapshotHost.Get(document, "Pages")).Select(p => SnapshotHost.Text(p, "Title")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string result = title; for (int i = 2; names.Contains(result); i++) result = title + " " + i;
        return result;
    }
    private static object Label(string text, double x, double y, double width, double height)
    {
        var label = Activator.CreateInstance(SnapshotHost.Type("Design.Graphics.TextBlock"))!;
        SnapshotHost.Set(label, "Label", text); SnapshotHost.Set(label, "Position", new Point(x, y));
        SnapshotHost.Set(label, "Size", new Size(width, height)); SnapshotHost.Set(label, "FontSize", 12.0);
        SnapshotHost.Set(label, "Color", Colors.White); SnapshotHost.Set(label, "Fill", Colors.Transparent);
        return label;
    }
    private static void CheckUnlocked(object item)
    {
        if (SnapshotHost.Get(item, "IsLocked") is true) throw new InvalidOperationException("The destination or a generated control is locked.");
    }
    private static void CheckDestination(object document, WorkspaceDestination destination)
    {
        if (destination.Kind == WorkspaceDestinationKind.NewPage) return;
        if (!SnapshotHost.Items(SnapshotHost.Get(document, "Pages")).Any(p => ReferenceEquals(p, destination.Page)))
            throw new InvalidOperationException("The destination page is no longer in this document.");
        if (destination.Popup is not null && !Objects(destination.Page!).Any(o => ReferenceEquals(o, destination.Popup)))
            throw new InvalidOperationException("The destination Popup is no longer on this page.");
        if (destination.Canvas is not null && SnapshotHost.Get(destination.Canvas, "IsAttached") is not true)
            throw new InvalidOperationException("The destination workspace is no longer attached.");
        CheckUnlocked(LayerFor((destination.Canvas ?? destination.Page)!));
        if (destination.Popup is not null)
        {
            CheckUnlocked(destination.Popup);
            if (SnapshotHost.Get(destination.Popup, "Layer") is { } popupLayer) CheckUnlocked(popupLayer);
        }
    }
    private static void Guard()
    {
        if (!Host.Enabled || SnapshotHost.Get(SnapshotHost.Type("Tools.Set"), "IsEditToolActive") is not true)
            throw new InvalidOperationException("Workspace edits require the supported Designer build in Design mode.");
        var popupType = SnapshotHost.Type("Design.Graphics.Popup");
        if (popupType.GetProperty("Content", SnapshotHost.Flags)?.PropertyType.FullName != "QSC.QSys.Design.Graphics.PopupPage")
            throw new InvalidOperationException("This Designer Popup contract is unsupported.");
    }
    private sealed class NativeTransaction(object manager) : IWorkspaceTransaction
    {
        public Guid Start()
        {
            foreach (string state in new[] { "IsLoading", "IsInUndo", "IsInEndOperation" })
                if (SnapshotHost.Get(manager, state) is true) throw new InvalidOperationException("Designer is busy. Try again after the current operation.");
            var pending = SnapshotHost.Get(manager, "_CurrentOp");
            if (SnapshotHost.Get(pending, "Count") is not int count || count != 0)
                throw new InvalidOperationException("Finish the current Designer edit before updating a workspace.");
            return (Guid)SnapshotHost.Call(manager, "StartOperation")!;
        }
        public void Finish(Guid operation) => SnapshotHost.Call(manager, "EndOperation", operation);
        public void Cancel() => SnapshotHost.Call(manager, "CancelOperation");
    }
}
