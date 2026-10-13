using ClosedXML.Excel;
using System.IO;

namespace mvp_project;

public sealed class ExcelService
{
    public bool Create(string path, DateTime productionDate, IEnumerable<PersonnelGroup> groups)
    {
        var template = Path.Combine(AppContext.BaseDirectory, "Templates", "template.xlsx");
        var hasTemplate = File.Exists(template);
        using var workbook = hasTemplate ? new XLWorkbook(template) : new XLWorkbook();
        Write(workbook, productionDate, groups);
        workbook.SaveAs(path);
        return hasTemplate;
    }

    private static void Write(XLWorkbook workbook, DateTime date, IEnumerable<PersonnelGroup> groups)
    {
        var sheet = workbook.Worksheets.FirstOrDefault() ?? workbook.AddWorksheet("Personalanforderung");
        sheet.Clear();
        var headers = new[] { "Datum Produktionsbeginn", "Name", "Vorname", "Von", "Bis", "Status" };
        sheet.Cell(1, 6).Value = DateTime.Today.ToString("dd.MM.yyyy");
        sheet.Cell(1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Cell(3, 1).InsertData(new[] { headers });

        var row = 4;
        var dateText = $"{GermanDay(date)} {date:dd.MM.yy}";
        foreach (var group in groups)
        {
            sheet.Cell(row, 1).Value = group.IsEmpty ? "" : dateText;
            sheet.Cell(row, 2).Value = group.Name;
            sheet.Cell(row, 3).Value = group.FirstName;
            sheet.Cell(row, 4).Value = group.StartTime;
            sheet.Cell(row, 5).Value = group.EndTime;
            sheet.Cell(row, 6).Value = group.Status;
            row++;
        }

        var printableTableEndRow = Math.Max(row - 1, 34);
        var footerRow = printableTableEndRow + 3;
        var used = sheet.Range(3, 1, Math.Max(row - 1, printableTableEndRow), headers.Length);
        used.Style.Font.FontName = "Segoe UI";
        used.Style.Font.FontSize = 10;
        used.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        used.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        used.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Row(3).Style.Font.Bold = true;
        sheet.Row(3).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE8F2");
        sheet.Row(3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Columns().AdjustToContents();
        sheet.Column(1).Width = 25;
        sheet.Column(2).Width = 14;
        sheet.Column(3).Width = 14;
        sheet.Column(4).Width = 10;
        sheet.Column(5).Width = 12;
        sheet.Column(6).Width = 18;
        sheet.Rows(4, printableTableEndRow).Height = 18;
        sheet.Cell(footerRow, 1).Value = "Versand";
        sheet.Cell(footerRow, 6).Value = $"Personalanforderung_{date:dd.MM.yy}.xlsx";
        sheet.Cell(footerRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Range(footerRow, 1, footerRow, 6).Style.Font.FontName = "Segoe UI";
        sheet.Range(footerRow, 1, footerRow, 6).Style.Font.FontSize = 10;
        sheet.PageSetup.PrintAreas.Clear();
        sheet.PageSetup.PrintAreas.Add($"A1:F{footerRow}");
        sheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.FitToPages(1, 1);
        sheet.PageSetup.Margins.Top = 0.35;
        sheet.PageSetup.Margins.Bottom = 0.35;
        sheet.PageSetup.Margins.Left = 0.35;
        sheet.PageSetup.Margins.Right = 0.35;
        sheet.SheetView.FreezeRows(1);
    }

    public static string GermanDay(DateTime date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => "Montag",
        DayOfWeek.Tuesday => "Dienstag",
        DayOfWeek.Wednesday => "Mittwoch",
        DayOfWeek.Thursday => "Donnerstag",
        DayOfWeek.Friday => "Freitag",
        DayOfWeek.Saturday => "Samstag",
        _ => "Sonntag"
    };
}
