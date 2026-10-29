using Designer.Modules.SnapshotInspector;

int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
void Reject(Action action, string message)
{
    try { action(); } catch (InvalidOperationException error) { Check(error.Message.Contains(message, StringComparison.Ordinal), error.Message); return; }
    throw new Exception("Expected rejection: " + message);
}
WorkspaceMember Member(string id, string control, params string[] banks) => new(new(id, control), id + "/" + control, banks);
WorkspaceItem[] Items(WorkspaceManifest manifest) => manifest.Controls.Select(m => new WorkspaceItem(m.Token, m.Key, false)).ToArray();
var gain = Member("component-a", "gain", "Bank 2", "Bank 1");
var mute = Member("component-a", "mute", "Bank 1");
var empty = WorkspacePlanner.Empty("canvas-one");
var first = WorkspacePlanner.Plan([mute, gain, gain with { Banks = ["Bank 3", "Bank 1"] }], empty, [], false);
Check(first.Added.Length == 2 && first.Next.Controls.Length == 2, "Shared memberships were not deduplicated.");
Check(first.Added[0].Key == gain.Key, "Stable source ordering failed.");
Check(first.Added[0].Banks.SequenceEqual(new[] { "Bank 1", "Bank 2", "Bank 3" }), "Bank aggregation failed.");
Check(first.Next.Controls.Select(m => m.Token).Distinct().Count() == 2, "Generated identities must be unique.");
var firstMembers = first.Added;
var repeated = WorkspacePlanner.Plan(firstMembers.Reverse(), first.Next, Items(first.Next), false);
Check(!repeated.Changed && repeated.Added.Length == 0 && repeated.Removed.Length == 0, "Repeated updates must be a no-op.");
Check(WorkspacePlanner.Encode(repeated.Next) == WorkspacePlanner.Encode(first.Next), "No-op rewrote identity or membership.");
var retired = WorkspacePlanner.Plan([gain], first.Next, Items(first.Next), false);
Check(retired.Removed.Length == 0 && retired.Retired.Single().Key == mute.Key, "Obsolete controls should be retained by default.");
Check(!retired.Next.Controls.Single(m => m.Key == mute.Key).Active, "Retired membership was not recorded.");
Check(retired.Next.Controls.Single(m => m.Key == mute.Key).Token == first.Next.Controls.Single(m => m.Key == mute.Key).Token, "Retirement lost identity.");
Check(!WorkspacePlanner.Plan([gain], retired.Next, Items(retired.Next), false).Changed, "Repeated retirement should not create another operation.");
var restored = WorkspacePlanner.Plan([gain, mute], retired.Next, Items(retired.Next), false);
Check(restored.Added.Length == 0 && restored.Next.Controls.All(m => m.Active), "Rejoining membership must reuse existing widgets.");
var removed = WorkspacePlanner.Plan([gain], first.Next, Items(first.Next), true);
Check(removed.Removed.Single().Key == mute.Key && removed.Next.Controls.Length == 1, "Explicit removal plan was wrong.");
Check(removed.Next.Controls[0].Token == first.Next.Controls.Single(m => m.Key == gain.Key).Token, "Retained identity changed during removal.");
var renamed = WorkspacePlanner.Plan([gain with { Caption = "new name", Banks = ["renamed bank"] }, mute], first.Next, Items(first.Next), false);
Check(renamed.Added.Length == 0 && renamed.Removed.Length == 0, "Renaming must not replace bound widgets.");
Check(renamed.Next.Controls.Single(m => m.Key == gain.Key).Banks.Single() == "renamed bank", "Membership labels did not refresh in metadata.");
Check(WorkspacePlanner.Normalize([Member("a_b", "c", "Bank"), Member("a", "b_c", "Bank")]).Length == 2, "Composite identity collided.");
Check(WorkspacePlanner.Normalize([Member("A", "gain", "Bank"), Member("a", "gain", "Bank")]).Length == 2, "Component identity lost case sensitivity.");
var noMembers = WorkspacePlanner.Plan([], first.Next, Items(first.Next), false);
Check(noMembers.Retired.Length == 2 && noMembers.Next.Controls.Length == 2, "Empty membership must preserve owner widgets by default.");
Check(WorkspacePlanner.Plan([], first.Next, Items(first.Next), true).Next.Controls.Length == 0, "Explicit full removal failed.");

Reject(() => WorkspacePlanner.Plan([gain], first.Next, [], false), "copied, deleted");
Reject(() => WorkspacePlanner.Plan([gain], first.Next, Items(first.Next).Concat([Items(first.Next)[0]]), false), "duplicated");
var rebound = Items(first.Next); rebound[0] = rebound[0] with { Key = new("different", "gain") };
Reject(() => WorkspacePlanner.Plan([gain], first.Next, rebound, true), "binding changed");
var locked = Items(first.Next).Select(i => i with { Locked = true }).ToArray();
Reject(() => WorkspacePlanner.Plan([gain], first.Next, locked, true), "locked");
Check(WorkspacePlanner.Plan([gain], first.Next, locked, false).Retired.Length == 1, "Keeping locked obsolete widgets should remain safe.");
Reject(() => WorkspacePlanner.Normalize([Member("", "gain", "Bank")]), "identity");
Reject(() => WorkspacePlanner.Normalize([Member("a", "gain", " ")]), "incomplete");
var maximum = Enumerable.Range(0, 1000).Select(i => Member("component", "control" + i, "Bank")).ToArray();
Check(WorkspacePlanner.Plan(maximum, empty, [], false).Added.Length == 1000, "Exact count boundary was rejected.");
Reject(() => WorkspacePlanner.Normalize(maximum.Concat([Member("component", "extra", "Bank")])), "1,000");
var full = WorkspacePlanner.Plan(maximum, empty, [], false).Next;
Reject(() => WorkspacePlanner.Plan([Member("component", "extra", "Bank")], full, Items(full), false), "Keeping obsolete");

string encoded = WorkspacePlanner.Encode(first.Next);
Check(WorkspacePlanner.Encode(WorkspacePlanner.Decode(encoded, "canvas-one")) == encoded, "Manifest round-trip changed identity.");
Reject(() => WorkspacePlanner.Decode(encoded, "copied-canvas"), "another canvas");
Reject(() => WorkspacePlanner.Decode("wrong" + encoded, "canvas-one"), "invalid ownership");
Reject(() => WorkspacePlanner.Decode(WorkspacePlanner.ManifestPrefix + "{", "canvas-one"), "damaged");
Reject(() => WorkspacePlanner.Decode(WorkspacePlanner.ManifestPrefix + "null", "canvas-one"), "unsupported");
Reject(() => WorkspacePlanner.Decode(WorkspacePlanner.Encode(first.Next with { Version = 2 }), "canvas-one"), "unsupported");
Reject(() => WorkspacePlanner.Decode(WorkspacePlanner.Encode(first.Next with { Controls = [first.Next.Controls[0], first.Next.Controls[0]] }), "canvas-one"), "duplicate");
Reject(() => WorkspacePlanner.Decode(new string('x', 2 * 1024 * 1024 + 1), "canvas-one"), "invalid ownership");
string unicodeCanvas = new('界', 800000);
string unicodeManifest = WorkspacePlanner.ManifestPrefix + "{\"Version\":1,\"WorkspaceId\":\"" + Guid.NewGuid().ToString("N") + "\",\"CanvasId\":\"" + unicodeCanvas + "\",\"Controls\":[]}";
Reject(() => WorkspacePlanner.Decode(unicodeManifest, unicodeCanvas), "invalid ownership");
Reject(() => WorkspacePlanner.Encode(first.Next with { CanvasId = new string('x', 2 * 1024 * 1024) }), "size limit");
string token = first.Next.Controls[0].Token;
Check(WorkspacePlanner.ControlToken("owner-style " + WorkspacePlanner.ControlPrefix + token + " another-style") == token, "Owner CSS classes obscured identity.");
Check(WorkspacePlanner.ControlToken("owner-style") is null, "Unowned objects must remain unowned.");
Reject(() => WorkspacePlanner.ControlToken(WorkspacePlanner.ControlPrefix + "bad"), "damaged");
Reject(() => WorkspacePlanner.ControlToken(WorkspacePlanner.ControlPrefix + token + " " + WorkspacePlanner.ControlPrefix + token), "ambiguous");
Check(WorkspacePlanner.NextRow(200, 100, 75) == 305, "Append position ignored existing owner layout.");
Reject(() => WorkspacePlanner.NextRow(double.NaN, 100, 50), "size range");
Reject(() => WorkspacePlanner.NextRow(0, 0, 50), "size range");
Reject(() => WorkspacePlanner.NextRow(99999, 100, 50), "size range");
Reject(() => WorkspacePlanner.NextRow(0, double.MaxValue, 50), "size range");
Check(WorkspacePlanner.ExistingBottom(-20, -30, 100, 10) == 0, "Negative owner positions must be preserved.");
Check(WorkspacePlanner.ExistingBottom(20, 100, 80, 0) == 100, "A horizontal line is a valid annotation.");
Check(WorkspacePlanner.ExistingBottom(20, 100, 0, 80) == 180, "A vertical line is a valid annotation.");
Reject(() => WorkspacePlanner.ExistingBottom(0, double.NaN, 1, 1), "unsupported layout");
Reject(() => WorkspacePlanner.ExistingBottom(0, 0, double.MaxValue, 1), "unsupported layout");

var host = new FakeHost();
WorkspaceTransaction.Apply(host, () => true, () => host.Value = 10, () => Check(host.Value == 10, "Apply did not run."));
Check(host.Events.SequenceEqual(new[] { "start", "finish" }) && host.History.Count == 1, "An update must be one undoable action.");
host.Undo(); Check(host.Value == 0, "Undo failed to restore original state.");
host.Redo(); Check(host.Value == 10, "Redo failed to restore the update.");
host = new FakeHost();
Reject(() => WorkspaceTransaction.Apply(host, () => false, () => host.Value = 1, () => { }), "changed");
Check(host.Events.Count == 0 && host.Value == 0, "Stale previews must not start a transaction.");
host = new FakeHost { Busy = true };
Reject(() => WorkspaceTransaction.Apply(host, () => true, () => host.Value = 1, () => { }), "active");
Check(host.Events.SequenceEqual(new[] { "start" }) && host.Value == 0, "Busy rejection touched an existing operation.");
host = new FakeHost(); int reads = 0;
Reject(() => WorkspaceTransaction.Apply(host, () => ++reads == 1, () => host.Value = 1, () => { }), "changed");
Check(host.Events.SequenceEqual(new[] { "start", "cancel", "finish" }) && host.History.Count == 0, "Late staleness was not cancelled.");
host = new FakeHost();
Reject(() => WorkspaceTransaction.Apply(host, () => true, () => { host.Value = 5; throw new InvalidOperationException("setter failure"); }, () => { }), "setter failure");
Check(host.Value == 0 && host.History.Count == 0, "Partial mutation must roll back.");
host = new FakeHost();
Reject(() => WorkspaceTransaction.Apply(host, () => true, () => host.Value = 5, () => throw new InvalidOperationException("verification failure")), "verification failure");
Check(host.Value == 0 && host.Events.SequenceEqual(new[] { "start", "cancel", "finish" }), "Verification failure did not roll back.");
host = new FakeHost(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
try { WorkspaceTransaction.Apply(host, () => true, () => host.Value = 1, () => { }, cancellation.Token); throw new Exception("Cancellation ignored."); }
catch (OperationCanceledException) { Check(host.Events.Count == 0, "Pre-cancel must not start an operation."); }
host = new FakeHost(); using var during = new CancellationTokenSource();
try { WorkspaceTransaction.Apply(host, () => true, () => { host.Value = 7; during.Cancel(); }, () => { }, during.Token); throw new Exception("Cancellation ignored."); }
catch (OperationCanceledException) { Check(host.Value == 0 && host.History.Count == 0, "In-operation cancellation did not roll back."); }
host = new FakeHost { FailFinish = true };
Reject(() => WorkspaceTransaction.Apply(host, () => true, () => host.Value = 5, () => { }), "uncertain");
Check(!host.Events.Contains("cancel") && host.Value == 5, "Uncertain commit must not blindly undo another operation.");
host = new FakeHost(); bool rollbackChecked = false;
Reject(() => WorkspaceTransaction.Apply(host, () => true, () => throw new InvalidOperationException("mutation failed"), () => { },
    verifyRollback: () => { Check(host.Value == 0, "Rollback state mismatch."); rollbackChecked = true; }), "mutation failed");
Check(rollbackChecked, "Native cancellation must be verified.");
host = new FakeHost(); int verifies = 0;
Reject(() => WorkspaceTransaction.Apply(host, () => true, () => host.Value = 5,
    () => { if (++verifies == 2) throw new InvalidOperationException("post-commit mismatch"); }), "uncertain");
Check(host.History.Count == 1 && !host.Events.Contains("cancel"), "Post-commit mismatch must not trigger blind rollback.");
Console.WriteLine($"{checks} workspace planning and transaction checks passed (portable; native adapter acceptance remains separate).");

sealed class FakeHost : IWorkspaceTransaction
{
    internal int Value;
    internal bool Busy, FailFinish;
    internal List<string> Events = [];
    internal List<(int Before, int After)> History = [];
    private int before;
    private Guid operation;
    public Guid Start() { Events.Add("start"); before = Value; return operation = Busy ? Guid.Empty : Guid.NewGuid(); }
    public void Finish(Guid id)
    {
        Events.Add("finish"); if (id != operation) throw new Exception("Wrong operation.");
        if (FailFinish) throw new InvalidOperationException("finish failure");
        if (Value != before) History.Add((before, Value));
    }
    public void Cancel() { Events.Add("cancel"); Value = before; }
    internal void Undo() => Value = History.Single().Before;
    internal void Redo() => Value = History.Single().After;
}
