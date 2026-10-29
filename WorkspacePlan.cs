using System.Text.Json;
using System.Text;

namespace Designer.Modules.SnapshotInspector;

internal sealed record WorkspaceKey(string ComponentId, string ControlId);
internal sealed record WorkspaceMember(WorkspaceKey Key, string Caption, string[] Banks);
internal sealed record WorkspaceOwned(string Token, WorkspaceKey Key, string[] Banks, bool Active);
internal sealed record WorkspaceManifest(int Version, string WorkspaceId, string CanvasId, WorkspaceOwned[] Controls);
internal sealed record WorkspaceItem(string Token, WorkspaceKey Key, bool Locked);
internal sealed record WorkspacePlan(WorkspaceMember[] Added, WorkspaceOwned[] Removed,
    WorkspaceOwned[] Retired, WorkspaceOwned[] Retained, WorkspaceManifest Next, bool Changed);

/// <summary>Pure membership planning. Existing widget values, geometry and styling are never rewritten.</summary>
internal static class WorkspacePlanner
{
    internal const int MaximumControls = 1000;
    internal const string ManifestPrefix = "Snapshot Inspector workspace v1\n";
    internal const string ManifestClass = "qsi-workspace-manifest-v1";
    internal const string ControlPrefix = "qsi-control-";

    internal static WorkspaceMember[] Normalize(IEnumerable<WorkspaceMember> source)
    {
        var rows = source.ToArray();
        if (rows.Length > 100000) throw new InvalidOperationException("Too many memberships to preview.");
        foreach (var member in rows)
        {
            ValidateKey(member.Key);
            if (member.Caption is null || member.Banks is null || member.Banks.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("A source membership is incomplete.");
        }
        var result = rows.GroupBy(m => m.Key).Select(group => new WorkspaceMember(group.Key,
            group.Select(m => m.Caption).Order(StringComparer.Ordinal).First(),
            group.SelectMany(m => m.Banks).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()))
            .OrderBy(m => m.Key.ComponentId, StringComparer.Ordinal).ThenBy(m => m.Key.ControlId, StringComparer.Ordinal).ToArray();
        if (result.Length > MaximumControls) throw new InvalidOperationException("Choose at most 1,000 distinct controls.");
        return result;
    }

    internal static WorkspaceManifest Empty(string canvasId) => new(1, Guid.NewGuid().ToString("N"), canvasId, []);
    internal static string Encode(WorkspaceManifest manifest)
    {
        string text = ManifestPrefix + JsonSerializer.Serialize(manifest);
        if (text.Length > 2 * 1024 * 1024 || Encoding.UTF8.GetByteCount(text) > 2 * 1024 * 1024)
            throw new InvalidOperationException("Workspace ownership information exceeds its size limit.");
        return text;
    }
    internal static WorkspaceManifest Decode(string text, string canvasId)
    {
        if (text.Length > 2 * 1024 * 1024 || !text.StartsWith(ManifestPrefix, StringComparison.Ordinal)
            || Encoding.UTF8.GetByteCount(text) > 2 * 1024 * 1024)
            throw new InvalidOperationException("This workspace has invalid ownership information.");
        WorkspaceManifest manifest;
        try { manifest = JsonSerializer.Deserialize<WorkspaceManifest>(text[ManifestPrefix.Length..])!; }
        catch (JsonException ex) { throw new InvalidOperationException("Workspace ownership information is damaged.", ex); }
        ValidateManifest(manifest, canvasId);
        return manifest;
    }

    internal static WorkspacePlan Plan(IEnumerable<WorkspaceMember> source, WorkspaceManifest manifest,
        IEnumerable<WorkspaceItem> current, bool removeObsolete)
    {
        ValidateManifest(manifest, manifest.CanvasId);
        var desired = Normalize(source);
        var items = current.ToArray();
        if (items.GroupBy(i => i.Token, StringComparer.Ordinal).Any(g => g.Count() != 1))
            throw new InvalidOperationException("Generated control identities are duplicated. Nothing can be updated safely.");
        var indexed = items.ToDictionary(i => i.Token, StringComparer.Ordinal);
        if (indexed.Count != manifest.Controls.Length)
            throw new InvalidOperationException("Generated controls were copied, deleted or had their ownership changed. Undo that edit or create a new workspace.");
        foreach (var owned in manifest.Controls)
            if (!indexed.TryGetValue(owned.Token, out var item) || item.Key != owned.Key)
                throw new InvalidOperationException("A generated control is missing or its binding changed. Nothing can be updated safely.");

        var desiredKeys = desired.Select(m => m.Key).ToHashSet();
        var existingKeys = manifest.Controls.Select(m => m.Key).ToHashSet();
        var added = desired.Where(m => !existingKeys.Contains(m.Key)).ToArray();
        var obsolete = manifest.Controls.Where(m => !desiredKeys.Contains(m.Key)).ToArray();
        var removed = removeObsolete ? obsolete : [];
        if (removed.Any(m => indexed[m.Token].Locked))
            throw new InvalidOperationException("An obsolete generated control is locked. Unlock it or keep obsolete controls.");
        var retained = manifest.Controls.Where(m => desiredKeys.Contains(m.Key)).ToArray();
        var byKey = desired.ToDictionary(m => m.Key);
        var next = retained.Select(m => m with { Banks = byKey[m.Key].Banks, Active = true })
            .Concat(removeObsolete ? [] : obsolete.Select(m => m with { Active = false }))
            .Concat(added.Select(m => new WorkspaceOwned(Guid.NewGuid().ToString("N"), m.Key, m.Banks, true)))
            .OrderBy(m => m.Key.ComponentId, StringComparer.Ordinal).ThenBy(m => m.Key.ControlId, StringComparer.Ordinal).ToArray();
        if (next.Length > MaximumControls)
            throw new InvalidOperationException("Keeping obsolete controls would exceed the 1,000-control workspace limit.");
        var nextManifest = manifest with { Controls = next };
        return new(added, removed, removeObsolete ? [] : obsolete, retained, nextManifest,
            Encode(nextManifest) != Encode(manifest));
    }

    internal static string? ControlToken(string classes)
    {
        var tokens = classes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(c => c.StartsWith(ControlPrefix, StringComparison.Ordinal)).ToArray();
        if (tokens.Length > 1) throw new InvalidOperationException("A control has ambiguous ownership information.");
        if (tokens.Length == 0) return null;
        string token = tokens[0][ControlPrefix.Length..];
        if (!Guid.TryParseExact(token, "N", out _)) throw new InvalidOperationException("A generated control identity is damaged.");
        return token;
    }

    internal static double NextRow(double bottom, double width, double height)
    {
        if (!double.IsFinite(bottom) || !double.IsFinite(width) || !double.IsFinite(height)
            || bottom < 0 || width <= 0 || width + 340 > 100000 || height <= 0 || bottom + Math.Max(50, height) + 30 > 100000)
            throw new InvalidOperationException("The workspace layout is outside the supported size range.");
        return Math.Ceiling(bottom + Math.Max(50, height) + 30);
    }

    internal static double ExistingBottom(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) || !double.IsFinite(height)
            || width < 0 || height < 0 || Math.Abs(x) + width > 100000 || Math.Abs(y) + height > 100000)
            throw new InvalidOperationException("An existing object has unsupported layout bounds.");
        return Math.Max(0, y + height);
    }

    private static void ValidateManifest(WorkspaceManifest? manifest, string canvasId)
    {
        if (manifest is null || manifest.Version != 1 || !Guid.TryParseExact(manifest.WorkspaceId, "N", out _)
            || string.IsNullOrWhiteSpace(canvasId) || manifest.CanvasId != canvasId || manifest.Controls is null
            || manifest.Controls.Length > MaximumControls)
            throw new InvalidOperationException("This workspace is unsupported or belongs to another canvas. Create a new workspace.");
        foreach (var owned in manifest.Controls)
        {
            if (owned is null || !Guid.TryParseExact(owned.Token, "N", out _) || owned.Banks is null
                || owned.Banks.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Workspace ownership information is invalid.");
            ValidateKey(owned.Key);
        }
        if (manifest.Controls.Select(m => m.Token).Distinct(StringComparer.Ordinal).Count() != manifest.Controls.Length
            || manifest.Controls.Select(m => m.Key).Distinct().Count() != manifest.Controls.Length)
            throw new InvalidOperationException("Workspace ownership information contains duplicate controls.");
    }

    private static void ValidateKey(WorkspaceKey? key)
    {
        if (key is null || string.IsNullOrWhiteSpace(key.ComponentId) || string.IsNullOrWhiteSpace(key.ControlId))
            throw new InvalidOperationException("A control identity is unavailable.");
    }
}
