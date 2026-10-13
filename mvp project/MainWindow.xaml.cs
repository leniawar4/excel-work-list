using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace mvp_project;

public partial class MainWindow : Window
{
    private int dragAnchor = -1;
    private bool draggingRows;
    private List<PersonnelGroup> editSelection = [];

    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;
        viewModel.GenerateRequested += GenerateRequested;
        GroupsGrid.LoadingRow += (_, args) => args.Row.Header = args.Row.GetIndex() + 1;
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private List<PersonnelGroup> SelectedRows() => GroupsGrid.SelectedItems.OfType<PersonnelGroup>().ToList();

    private void GroupsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedCount = GroupsGrid.SelectedItems.Count;
    }

    private void AddRowClick(object sender, RoutedEventArgs e)
    {
        ViewModel.AddRowCommand.Execute(null);
        GroupsGrid.SelectedIndex = GroupsGrid.Items.Count - 1;
    }

    private void InsertBlankClick(object sender, RoutedEventArgs e)
    {
        var selected = SelectedRows();
        var index = selected.Count == 0 ? GroupsGrid.Items.Count : GroupsGrid.Items.IndexOf(selected[^1]) + 1;
        ViewModel.InsertBlankRow(index);
        GroupsGrid.SelectedIndex = index;
    }

    private void DuplicateClick(object sender, RoutedEventArgs e) => ViewModel.DuplicateRows(SelectedRows());
    private void ClearClick(object sender, RoutedEventArgs e) => ViewModel.ClearRows(SelectedRows());

    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        var selected = SelectedRows();
        ViewModel.RemoveRows(selected);
        ViewModel.SelectedCount = 0;
    }

    private void ClearColumnClick(object sender, RoutedEventArgs e)
    {
        var column = Convert.ToString(GroupsGrid.CurrentCell.Column?.Header);
        if (!string.IsNullOrWhiteSpace(column))
            ViewModel.ClearColumn(column);
    }

    private void ApplyBulkClick(object sender, RoutedEventArgs e) => ViewModel.ApplyBulkEdit(SelectedRows());

    private void GridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        var header = Convert.ToString(e.Column.Header);
        if (header is not ("Von" or "Bis") || e.Row.Item is not PersonnelGroup editedRow) return;

        var targets = editSelection.Count > 1 ? editSelection : SelectedRows();
        if (targets.Count <= 1) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            var value = header == "Von" ? editedRow.StartTime : editedRow.EndTime;
            foreach (var row in targets)
            {
                if (header == "Von") row.StartTime = value;
                else row.EndTime = value;
            }
            editSelection.Clear();
        });
    }

    private void StatusSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: "(leer)" } combo && combo.DataContext is PersonnelGroup row)
            row.Status = "";
    }

    private void GroupsGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control || !Clipboard.ContainsText()) return;
        var lines = Clipboard.GetText().Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return;

        var start = GroupsGrid.SelectedItems.OfType<PersonnelGroup>().Select(GroupsGrid.Items.IndexOf).DefaultIfEmpty(0).Min();
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            while (start + lineIndex >= ViewModel.Groups.Count)
                ViewModel.AddRowCommand.Execute(null);
            var values = lines[lineIndex].Split('\t');
            var row = ViewModel.Groups[start + lineIndex];
            var offset = values.Length >= 6 ? 1 : 0;
            if (values.Length >= 4 && offset == 0)
            {
                row.Name = values[0]; row.StartTime = values[1]; row.EndTime = values[2]; row.Status = values[3];
            }
            else if (values.Length >= 5)
            {
                row.Name = values[offset + 1]; row.FirstName = values[offset + 2];
                row.StartTime = values[offset + 3]; row.EndTime = values[offset + 4];
                row.Status = values.Length > offset + 5 ? values[offset + 5] : "";
            }
        }
        e.Handled = true;
    }

    private void RowPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row || row.Item is not PersonnelGroup item) return;
        if (FindParent<DataGridCell>(e.OriginalSource as DependencyObject) is not null)
        {
            editSelection = SelectedRows();
            return;
        }
        dragAnchor = GroupsGrid.Items.IndexOf(item);
        draggingRows = true;
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == ModifierKeys.None)
            GroupsGrid.SelectedItems.Clear();
        GroupsGrid.SelectedItems.Add(item);
    }

    private static T? FindParent<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match) return match;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void RowPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!draggingRows || e.LeftButton != MouseButtonState.Pressed || dragAnchor < 0) return;
        if (sender is not DataGridRow row || row.Item is not PersonnelGroup item) return;
        var current = GroupsGrid.Items.IndexOf(item);
        if (current < 0) return;
        var first = Math.Min(dragAnchor, current);
        var last = Math.Max(dragAnchor, current);
        GroupsGrid.SelectedItems.Clear();
        for (var index = first; index <= last; index++)
            GroupsGrid.SelectedItems.Add(GroupsGrid.Items[index]);
    }

    private void GenerateClick(object sender, RoutedEventArgs e)
    {
        GenerateRequested(this, EventArgs.Empty);
    }

    private async void GenerateRequested(object? sender, EventArgs args)
    {
        GroupsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        GroupsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var dialog = new SaveFileDialog
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            FileName = $"Personalanforderung_{ViewModel.ProductionDate:dd.MM.yy}.xlsx",
            InitialDirectory = ViewModel.OutputDirectory,
            AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;
        ViewModel.OutputDirectory = Path.GetDirectoryName(dialog.FileName) ?? ViewModel.OutputDirectory;
        await ViewModel.GenerateAsync(dialog.FileName);
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        draggingRows = false;
        base.OnPreviewMouseUp(e);
    }
}
