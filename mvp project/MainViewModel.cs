using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace mvp_project;

public partial class MainViewModel : ObservableObject
{
    private readonly ExcelService excelService = new();
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PersonnelGenerator", "settings.json");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GermanDate))]
    private DateTime productionDate = DateTime.Today;

    [ObservableProperty]
    private string bulkStartTime = "";

    [ObservableProperty]
    private string bulkEndTime = "";

    [ObservableProperty]
    private string bulkStatus = "";

    [ObservableProperty]
    private string message = "";

    [ObservableProperty]
    private string outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public ObservableCollection<PersonnelGroup> Groups { get; } = new();
    public string GermanDate => $"{ExcelService.GermanDay(ProductionDate)} {ProductionDate:dd.MM.yy}";
    public int TotalPersonnel => Groups.Count(group => !group.IsEmpty);
    [ObservableProperty]
    private int selectedCount;
    public event EventHandler? GenerateRequested;

    public MainViewModel()
    {
        LoadSettings();
        foreach (var group in Groups)
            group.PropertyChanged += GroupPropertyChanged;
        if (Groups.Count == 0)
            AddDefaults();
        while (Groups.Count < 24)
            AddRow();
    }

    [RelayCommand]
    private void AddRow()
    {
        var group = new PersonnelGroup();
        Groups.Add(group);
        group.PropertyChanged += GroupPropertyChanged;
        OnPropertyChanged(nameof(TotalPersonnel));
    }

    public void InsertBlankRow(int index)
    {
        Groups.Insert(Math.Clamp(index, 0, Groups.Count), new PersonnelGroup());
        OnPropertyChanged(nameof(TotalPersonnel));
    }

    public void RemoveRows(IEnumerable<PersonnelGroup> rows)
    {
        foreach (var row in rows.Distinct().ToList())
        {
            row.PropertyChanged -= GroupPropertyChanged;
            Groups.Remove(row);
        }
        while (Groups.Count < 1)
            AddRow();
        OnPropertyChanged(nameof(TotalPersonnel));
    }

    public void ClearRows(IEnumerable<PersonnelGroup> rows)
    {
        foreach (var row in rows.Distinct())
        {
            row.Name = "";
            row.FirstName = "";
            row.StartTime = "";
            row.EndTime = "";
            row.Status = "";
        }
        OnPropertyChanged(nameof(TotalPersonnel));
    }

    public void DuplicateRows(IEnumerable<PersonnelGroup> rows)
    {
        var selected = rows.Distinct().ToList();
        var insertAt = selected.Count == 0 ? Groups.Count : Groups.IndexOf(selected[^1]) + 1;
        foreach (var row in selected)
        {
            var copy = new PersonnelGroup
            {
                Name = row.Name, FirstName = row.FirstName, StartTime = row.StartTime,
                EndTime = row.EndTime, Status = row.Status
            };
            Groups.Insert(insertAt++, copy);
            copy.PropertyChanged += GroupPropertyChanged;
        }
        OnPropertyChanged(nameof(TotalPersonnel));
    }

    public void ApplyBulkEdit(IEnumerable<PersonnelGroup> rows)
    {
        foreach (var row in rows.Distinct())
        {
            if (!string.IsNullOrWhiteSpace(BulkStartTime)) row.StartTime = BulkStartTime.Trim();
            if (!string.IsNullOrWhiteSpace(BulkEndTime)) row.EndTime = BulkEndTime.Trim();
            if (!string.IsNullOrWhiteSpace(BulkStatus)) row.Status = BulkStatus == "(leer)" ? "" : BulkStatus;
        }
        OnPropertyChanged(nameof(TotalPersonnel));
    }

    public void ClearColumn(string column)
    {
        foreach (var row in Groups)
            row.ClearColumn(column);
        OnPropertyChanged(nameof(TotalPersonnel));
    }

    [RelayCommand]
    private void RequestGenerate() => GenerateRequested?.Invoke(this, EventArgs.Empty);

    public async Task GenerateAsync(string path)
    {
        Message = "";
        var groups = Groups.ToList();
        try
        {
            var usedTemplate = await Task.Run(() => excelService.Create(path, ProductionDate, groups));
            SaveSettings();
            Message = usedTemplate
                ? $"Excel file created successfully: {Path.GetFileName(path)}"
                : $"Excel file created successfully: {Path.GetFileName(path)} (template not found; standard layout used)";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Message = "The file could not be saved. Close it if it is open and check the folder permissions.";
        }
        catch (Exception)
        {
            Message = "Excel generation failed. Check the template and try again.";
        }
    }

    private void GroupPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(PersonnelGroup.Name) or nameof(PersonnelGroup.FirstName)
            or nameof(PersonnelGroup.StartTime) or nameof(PersonnelGroup.EndTime) or nameof(PersonnelGroup.Status))
            OnPropertyChanged(nameof(TotalPersonnel));
    }

    private void AddDefaults()
    {
        Groups.Add(new PersonnelGroup { Name = "FARA", StartTime = "09:00", EndTime = "ca. 16:00", Status = "PAL" });
        Groups.Add(new PersonnelGroup { Name = "MHL", FirstName = "CIOBANU", StartTime = "09:00", EndTime = "ca. 16:00" });
        Groups.Add(new PersonnelGroup { Name = "FARA", StartTime = "10:30", EndTime = "ca. 16:00" });
        Groups.Add(new PersonnelGroup());
        Groups.Add(new PersonnelGroup { Name = "FARA", StartTime = "16:00", EndTime = "ca. 19:00", Status = "TAZ" });
        Groups.Add(new PersonnelGroup { Name = "FARA", StartTime = "21:00", EndTime = "ENDE" });
        Groups.Add(new PersonnelGroup { Name = "GAZ", StartTime = "20:00", EndTime = "ENDE" });
        foreach (var group in Groups) group.PropertyChanged += GroupPropertyChanged;
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(settingsPath)) return;
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath));
            if (settings is null) return;
            OutputDirectory = Directory.Exists(settings.OutputDirectory) ? settings.OutputDirectory : OutputDirectory;
            foreach (var group in settings.Groups ?? [])
            {
                Groups.Add(group);
            }
        }
        catch (Exception) { }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new Settings(OutputDirectory, Groups)));
        }
        catch (Exception) { }
    }

    private sealed record Settings(string OutputDirectory, ObservableCollection<PersonnelGroup> Groups);
}
