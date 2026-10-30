using Designer.Modules.SnapshotInspector;
using QSC.QSys;
using QSC.QSys.Design;
using QSC.QSys.Design.Graphics;
using System.Windows;
using System.Windows.Media;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
void Reject(Action action, string text)
{
    try { action(); } catch (Exception ex) { Check(ex.GetBaseException().Message.Contains(text, StringComparison.OrdinalIgnoreCase), ex.ToString()); return; }
    throw new Exception("Expected rejection: " + text);
}
var component = new Component { IsAttached = true };
var bank = new object();
SnapshotRow Row(string control) => new(bank, component, new Descriptor(control), new(component.Id, control), "Bank A", "Gain", "Schematic", control, control, "Individual", control);
SnapshotRow[] members = [Row("gain"), Row("mute"), Row("gain")];
var existingPage = new Canvas(); App.Manager.Document.Pages.Add(existingPage);
var detachedChild = new TextBlock();
existingPage.Layers[0].AddChild(detachedChild);
Check(!detachedChild.IsAttached, "Adding a child to an attached layer must not implicitly attach it.");
detachedChild.Attach();
Check(detachedChild.IsAttached, "Explicit element attachment must remain available independently of layer insertion.");
existingPage.Layers[0].RemoveChild(detachedChild);
var target = new WorkspaceDestination("new page", WorkspaceDestinationKind.NewPage);
var preview = WorkspaceAdapter.Prepare(target, members, "Bank A", false);
Check(ComponentHelper.Calls == 0 && App.Manager.Starts == 0, "Preview must not construct controls or start an operation.");
Check(SnapshotHost.Items(App.Manager.Document.Pages).Length == 1, "Preview must not add a page.");
WorkspaceAdapter.Apply(preview, () => members);
var page = SnapshotHost.Items(App.Manager.Document.Pages).Cast<Canvas>().Last();
var controls = page.AllChildren.OfType<ControlStyle>().ToArray();
Check(controls.Length == 2 && ComponentHelper.Calls == 2, "Overlapping memberships must produce exactly two native widgets.");
Check(controls.All(w => w.IsAttached && w.Value.Id == component.Id && w.CssClassName.Contains("owner-source-style")), "Attachment, native binding or existing CSS classes were lost.");
Check(App.Manager.Commits == 1, "Creation must commit once.");
Check(page.PageBackgroundColor == 0 && page.AllChildren.OfType<TextBlock>().All(t => t.Color == Colors.Black), "Generated pages must retain native background and black model text.");
var update = WorkspaceAdapter.Destinations().Single(d => d.Kind == WorkspaceDestinationKind.Update && ReferenceEquals(d.Canvas, page));
var unchanged = WorkspaceAdapter.Prepare(update, members, "Bank A", false);
Check(!unchanged.Plan.Changed, "Repeated membership must be a no-op.");
int starts = App.Manager.Starts;
WorkspaceAdapter.Apply(unchanged, () => members);
Check(App.Manager.Starts == starts, "No-op must not create an operation.");
var gain = controls.Single(w => w.Value.Name == "gain");
gain.Position = new(800, 500); gain.LocalStyleString = "owner-style";
var annotation = new TextBlock { Label = "Owner annotation", Position = new(-10, 30) }; page.Layers[0].AddChild(annotation); annotation.Attach();
members = [Row("gain"), Row("mute"), Row("new")];
WorkspaceAdapter.Apply(WorkspaceAdapter.Prepare(update, members, "Bank A", false), () => members);
Check(page.AllChildren.Contains(gain) && gain.Position == new Point(800, 500) && gain.LocalStyleString == "owner-style", "Update altered retained identity, position or styling.");
Check(page.AllChildren.Contains(annotation) && annotation.Label == "Owner annotation", "Update removed an annotation.");
Check(page.AllChildren.OfType<ControlStyle>().Single(w => w.Value.Name == "new").Position.Y > 500, "New widgets must append below existing layout.");
Check(page.AllChildren.All(o => o.IsAttached), "Updates must attach appended controls, captions and ownership metadata.");
members = [Row("gain")];
WorkspaceAdapter.Apply(WorkspaceAdapter.Prepare(update, members, "Bank A", false), () => members);
Check(page.AllChildren.OfType<ControlStyle>().Count() == 3, "Default retirement removed an existing widget.");
members = [Row("gain"), Row("mute"), Row("new")];
WorkspaceAdapter.Apply(WorkspaceAdapter.Prepare(update, members, "Bank A", false), () => members);
Check(page.AllChildren.Contains(controls.Single(w => w.Value.Name == "mute")), "Rejoining membership must reuse the original widget.");
members = [Row("gain")];
int labels = page.AllChildren.OfType<TextBlock>().Count();
WorkspaceAdapter.Apply(WorkspaceAdapter.Prepare(update, members, "Bank A", true), () => members);
Check(page.AllChildren.OfType<ControlStyle>().Single() == gain && page.AllChildren.OfType<TextBlock>().Count() == labels, "Explicit removal must preserve all captions and annotations.");
var stale = WorkspaceAdapter.Prepare(update, [Row("gain"), Row("new")], "Bank A", false);
gain.LocalStyleString = "edited-after-preview";
starts = App.Manager.Starts;
Reject(() => WorkspaceAdapter.Apply(stale, () => [Row("gain"), Row("new")]), "changed");
Check(App.Manager.Starts == starts, "Stale styling must reject before a transaction.");
page.IsLocked = true;
Reject(() => WorkspaceAdapter.Prepare(update, members, "Bank A", false), "locked");
page.IsLocked = false;
var before = page.AllChildren.ToArray();
string markerBefore = page.AllChildren.OfType<TextBlock>().Single(t => t.IsHidden).Label;
members = [Row("gain"), Row("new1"), Row("new2")];
var failed = WorkspaceAdapter.Prepare(update, members, "Bank A", false);
ComponentHelper.FailAt = ComponentHelper.Calls + 2;
Reject(() => WorkspaceAdapter.Apply(failed, () => members), "factory failure");
Check(page.AllChildren.SequenceEqual(before) && page.AllChildren.OfType<TextBlock>().Single(t => t.IsHidden).Label == markerBefore, "Failed apply must restore the existing scene and ownership metadata.");
Check(App.Manager.Cancels == 1 && !App.Manager.Active, "Failed apply did not cancel its own operation.");
ComponentHelper.FailAt = 0;
var cancellation = new CancellationToken(true);
starts = App.Manager.Starts;
Reject(() => WorkspaceAdapter.Apply(failed, () => members, cancellation), "canceled");
Check(App.Manager.Starts == starts, "Cancellation must not start an operation.");
App.Manager.OnStart = () => QSC.QSys.Tools.Set.IsEditToolActive = false;
Reject(() => WorkspaceAdapter.Apply(failed, () => members), "changed");
Check(page.AllChildren.SequenceEqual(before), "Mode change after transaction start mutated the scene.");
App.Manager.OnStart = null; QSC.QSys.Tools.Set.IsEditToolActive = true;
members = [Row("gain"), Row("mute")];
var popupTarget = new WorkspaceDestination("new Popup", WorkspaceDestinationKind.NewPopup, existingPage);
WorkspaceAdapter.Apply(WorkspaceAdapter.Prepare(popupTarget, members, "Bank A", false), () => members);
var popup = existingPage.AllChildren.OfType<Popup>().Single();
Check(popup.IsAttached && popup.Content.IsAttached && popup.Content.AllChildren.OfType<ControlStyle>().Count() == 2, "Popup must attach controls through its native content canvas.");
Check(popup.Content.AllChildren.All(o => o.IsAttached), "Popup attachment must propagate to every control, caption and ownership marker.");
Check(popup.Content.Color == new PopupPage().Color && popup.Color == Colors.Black, "Popup creation must retain native background defaults and black model text.");
Check(popup.Content.AllChildren.All(o => o.IsHidden || o.Position.X + o.Size.Width <= popup.ContentSize.Width && o.Position.Y + o.Size.Height <= popup.ContentSize.Height), "Popup bounds clip its generated contents.");
var popupUpdate = WorkspaceAdapter.Destinations().Single(d => ReferenceEquals(d.Popup, popup));
var originalPopupControls = popup.Content.AllChildren.OfType<ControlStyle>().ToArray();
var oldSize = popup.ContentSize;
members = [Row("gain"), Row("mute"), Row("new")];
WorkspaceAdapter.Apply(WorkspaceAdapter.Prepare(popupUpdate, members, "Bank A", false), () => members);
Check(originalPopupControls.All(c => popup.Content.AllChildren.Contains(c)) && popup.ContentSize.Height > oldSize.Height, "Popup update must retain widgets and grow for additions.");
Check(popup.Content.AllChildren.All(o => o.IsAttached), "Updates to existing Popups must attach their appended controls and captions.");
var grownSize = popup.ContentSize;
members = [Row("gain")];
WorkspaceAdapter.Apply(WorkspaceAdapter.Prepare(popupUpdate, members, "Bank A", true), () => members);
Check(popup.ContentSize == grownSize, "Popup removal must not shrink owner layout.");
Check(!WorkspaceAdapter.Prepare(popupUpdate, members, "Bank A", false).Plan.Changed, "Popup update must converge to a no-op.");

foreach (var attachedTarget in new[] { update, popupUpdate })
{
    var attachedCanvas = (Canvas)attachedTarget.Canvas!;
    var beforeFailure = attachedCanvas.AllChildren.ToArray();
    var beforeMarker = attachedCanvas.AllChildren.OfType<TextBlock>().Single(t => t.IsHidden).Label;
    var beforeOperations = (App.Manager.Commits, App.Manager.Cancels);
    members = [Row("gain"), Row("attachment-failure")];
    var attachmentPreview = WorkspaceAdapter.Prepare(attachedTarget, members, "Bank A", false);
    Element.AttachFailure = element => element is ControlStyle { Value.Name: "attachment-failure" };
    try { Reject(() => WorkspaceAdapter.Apply(attachmentPreview, () => members), "attachment failure"); }
    finally { Element.AttachFailure = null; }
    Check(attachedCanvas.AllChildren.SequenceEqual(beforeFailure) && beforeFailure.All(o => o.IsAttached)
        && attachedCanvas.AllChildren.OfType<TextBlock>().Single(t => t.IsHidden).Label == beforeMarker,
        "An attachment failure must restore retained objects, attachment state and ownership metadata.");
    Check(App.Manager.Commits == beforeOperations.Commits && App.Manager.Cancels == beforeOperations.Cancels + 1 && !App.Manager.Active,
        "An attachment failure must cancel its operation without committing.");
}
var beforePopupFailure = existingPage.AllChildren.ToArray();
var priorCommits = App.Manager.Commits;
members = [Row("gain"), Row("mute")];
var popupFailurePreview = WorkspaceAdapter.Prepare(popupTarget, members, "Bank A", false);
Element.AttachFailure = element => element is Popup;
try { Reject(() => WorkspaceAdapter.Apply(popupFailurePreview, () => members), "attachment failure"); }
finally { Element.AttachFailure = null; }
Check(existingPage.AllChildren.SequenceEqual(beforePopupFailure) && App.Manager.Commits == priorCommits && !App.Manager.Active,
    "A failed Popup attachment must leave no Popup or committed operation.");
Console.WriteLine($"{checks} workspace adapter contract checks passed (portable fixture; not native Designer acceptance).");
