using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using QsysMods.Core;

namespace Designer.Modules.SnapshotInspector;

internal sealed class WorkspaceWindow : Window
{
    private readonly ComboBox destination = new() { MinWidth = 360, Margin = new Thickness(0, 0, 0, 10) };
    private readonly CheckBox remove = new() { Content = "Remove obsolete generated controls (keep all labels and annotations)", Margin = new Thickness(0, 0, 0, 10) };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    private readonly Button apply = new() { Content = "Apply update", IsEnabled = false, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(8, 0, 0, 0) };
    private readonly DataGrid rows = new() { IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false };
    private readonly Func<SnapshotRow[]> readRows;
    private readonly string workspaceTitle;
    private WorkspaceAdapter.Preview? preview;
    internal string? Result { get; private set; }

    internal WorkspaceWindow(Window owner, Func<SnapshotRow[]> readRows, string title)
    {
        this.readRows = readRows; workspaceTitle = title;
        Owner = owner; Title = "Create or update snapshot workspace"; Width = 920; Height = 590; MinWidth = 650; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Host.Theme(this);
        var root = new DockPanel { Margin = new Thickness(14) }; Content = root;
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "Destination", Margin = new Thickness(0, 0, 0, 4) }); top.Children.Add(destination); top.Children.Add(remove);
        top.Children.Add(new TextBlock { Text = "Existing controls keep their positions, styles and bindings. New controls are appended below the current layout; a Popup grows if needed. Obsolete controls are kept unless removal is selected.", TextWrapping = TextWrapping.Wrap });
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); footer.Children.Add(status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; footer.Children.Add(buttons);
        var refresh = new Button { Content = "Refresh preview", Padding = new Thickness(14, 5, 14, 5) }; buttons.Children.Add(refresh); buttons.Children.Add(apply);
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(8, 0, 0, 0) }; buttons.Children.Add(cancel);
        foreach (var (caption, property, width) in new[] { ("Change", "Change", 140.0), ("Control", "Caption", 470.0), ("Banks", "Banks", 230.0) })
            rows.Columns.Add(new DataGridTextColumn { Header = caption, Binding = new Binding(property), Width = width });
        root.Children.Add(rows);
        destination.ItemsSource = WorkspaceAdapter.Destinations(); destination.SelectedIndex = 0;
        destination.SelectionChanged += (_, _) => Refresh(); remove.Checked += (_, _) => Refresh(); remove.Unchecked += (_, _) => Refresh();
        refresh.Click += (_, _) => Refresh(); apply.Click += (_, _) => Apply();
        Loaded += (_, _) => Refresh();
    }

    private sealed record PreviewRow(string Change, string Caption, string Banks);
    private void Refresh()
    {
        preview = null; apply.IsEnabled = false; rows.ItemsSource = null;
        try
        {
            if (destination.SelectedItem is not WorkspaceDestination target) return;
            var value = WorkspaceAdapter.Prepare(target, readRows(), workspaceTitle, remove.IsChecked is true);
            var members = value.Members.ToDictionary(m => m.Key);
            string Caption(WorkspaceOwned owned) => members.TryGetValue(owned.Key, out var member) ? member.Caption
                : owned.Key.ComponentId + " / " + owned.Key.ControlId;
            rows.ItemsSource = value.Plan.Added.Select(m => new PreviewRow("Add", m.Caption, string.Join(", ", m.Banks)))
                .Concat(value.Plan.Removed.Select(m => new PreviewRow("Remove widget", Caption(m), string.Join(", ", m.Banks))))
                .Concat(value.Plan.Retired.Select(m => new PreviewRow("Keep obsolete", Caption(m), string.Join(", ", m.Banks))))
                .Concat(value.Plan.Retained.Select(m => new PreviewRow("Keep", Caption(m), string.Join(", ", members[m.Key].Banks)))).ToArray();
            preview = value; apply.IsEnabled = value.Plan.Changed;
            status.Text = value.Plan.Changed
                ? $"{value.Plan.Added.Length} added, {value.Plan.Removed.Length} removed, {value.Plan.Retained.Length} retained, {value.Plan.Retired.Length} obsolete kept. Review before applying."
                : "No membership changes. The workspace is already up to date.";
        }
        catch (Exception ex) { status.Text = ex.GetBaseException().Message; }
    }

    private void Apply()
    {
        if (preview is not { } prepared) return;
        apply.IsEnabled = false;
        try { Result = WorkspaceAdapter.Apply(prepared, readRows); DialogResult = true; }
        catch (WorkspaceCommitUncertainException ex) { preview = null; status.Text = ex.Message; }
        catch (AggregateException ex) { preview = null; status.Text = ex.Message; }
        catch (Exception ex) { preview = null; status.Text = ex.Message + " Refresh the preview before trying again."; }
    }
}
