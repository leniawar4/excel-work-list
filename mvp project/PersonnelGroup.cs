using CommunityToolkit.Mvvm.ComponentModel;

namespace mvp_project;

public partial class PersonnelGroup : ObservableObject
{
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string firstName = "";
    [ObservableProperty] private string startTime = "";
    [ObservableProperty] private string endTime = "";
    [ObservableProperty] private string status = "";

    public bool IsEmpty => string.IsNullOrWhiteSpace(Name)
        && string.IsNullOrWhiteSpace(FirstName)
        && string.IsNullOrWhiteSpace(StartTime)
        && string.IsNullOrWhiteSpace(EndTime)
        && string.IsNullOrWhiteSpace(Status);

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnFirstNameChanged(string value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnStartTimeChanged(string value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnEndTimeChanged(string value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(IsEmpty));

    public void ClearColumn(string column)
    {
        switch (column)
        {
            case "Name": Name = ""; break;
            case "Vorname": FirstName = ""; break;
            case "Von": StartTime = ""; break;
            case "Bis": EndTime = ""; break;
            case "Status": Status = ""; break;
        }
    }
}
