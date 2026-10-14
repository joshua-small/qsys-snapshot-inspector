using QsysMods.Core;

namespace Designer.Modules.SnapshotInspector;
public sealed class SnapshotModule : ModModule
{
    public SnapshotModule() => Mods.Register("snapshot-inspector", _ =>
    {
        SnapshotTools.Install((kind, detail) => Host.Receipt("snapshot-inspector", kind, detail));
        Commands.Register(new("snapshot-inspector/window", "Snapshot Inspector…", "Snapshot Inspector", () => SnapshotTools.Show(), () => true, InMenu: true));
    });
}
