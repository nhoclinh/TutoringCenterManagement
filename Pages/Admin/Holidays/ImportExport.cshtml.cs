using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;

namespace TutoringCenterManagement.Pages.Admin.Holidays
{
    public class ImportExportModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ImportExportModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ── Trạng thái kết quả ──
        public bool ShowResult { get; set; }
        public int ImportSuccess { get; set; }
        public int ImportFailed { get; set; }
        public List<string> ImportErrors { get; set; } = new();

        // ── Số bản ghi hiện tại (hiển thị trên nút Export) ──
        public int TotalHolidays { get; set; }

        /* ════════════════════════════════════
           GET
        ════════════════════════════════════ */
        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            TotalHolidays = await _context.Holidays.CountAsync();
            return Page();
        }

        /* ════════════════════════════════════
           EXPORT — Tải xuống file Excel
        ════════════════════════════════════ */
        public async Task<IActionResult> OnGetExportAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var holidays = await _context.Holidays
                .OrderBy(h => h.StartDate)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Holidays");

            // ── Header ──
            var headers = new[] { "HolidayName", "StartDate", "EndDate", "Description" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2d3a5c");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Highlight cột bắt buộc (vàng nhạt)
            foreach (int req in new[] { 1, 2, 3 })
                ws.Cell(1, req).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");

            // ── Dữ liệu ──
            int row = 2;
            foreach (var h in holidays)
            {
                ws.Cell(row, 1).Value = h.HolidayName;
                ws.Cell(row, 2).Value = h.StartDate.ToString("dd/MM/yyyy");
                ws.Cell(row, 3).Value = h.EndDate.ToString("dd/MM/yyyy");
                ws.Cell(row, 4).Value = h.Description ?? "";

                // Zebra stripe
                if (row % 2 == 0)
                {
                    var range = ws.Range(row, 1, row, 4);
                    range.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fc");
                }
                row++;
            }

            // ── Freeze header + auto-width ──
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();
            ws.Column(1).Width = Math.Max(ws.Column(1).Width, 30);
            ws.Column(4).Width = Math.Max(ws.Column(4).Width, 40);

            // ── Border ngoài ──
            if (row > 2)
            {
                var dataRange = ws.Range(1, 1, row - 1, 4);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#d0d5e8");
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#e8eaf2");
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;

            var fileName = $"Holidays_Export_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        /* ════════════════════════════════════
           TEMPLATE — File mẫu để import
        ════════════════════════════════════ */
        public IActionResult OnGetTemplateAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Holidays");

            // ── Header ──
            var colDefs = new[]
            {
                ("HolidayName", true,  "Tên ngày lễ (bắt buộc)"),
                ("StartDate",   true,  "Ngày bắt đầu dd/MM/yyyy (bắt buộc)"),
                ("EndDate",     true,  "Ngày kết thúc dd/MM/yyyy (bắt buộc)"),
                ("Description", false, "Mô tả thêm (tuỳ chọn)"),
            };

            for (int c = 0; c < colDefs.Length; c++)
            {
                var (name, required, comment) = colDefs[c];
                var cell = ws.Cell(1, c + 1);
                cell.Value = name;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = required
                    ? XLColor.FromHtml("#FFF2CC")   // vàng = bắt buộc
                    : XLColor.FromHtml("#f0f4ff");   // xanh nhạt = tuỳ chọn
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Ghi chú vào comment
                var note = ws.Cell(1, c + 1).CreateComment();
                note.AddText(comment);
            }

            // ── Freeze + width ──
            ws.SheetView.FreezeRows(1);
            ws.Column(1).Width = 35;
            ws.Column(2).Width = 22;
            ws.Column(3).Width = 22;
            ws.Column(4).Width = 45;

            // ── Border header ──
            var headerRange = ws.Range(1, 1, 1, 4);
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            headerRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#b0b8d0");
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#d0d5e8");

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;

            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Holidays_Template.xlsx");
        }

        /* ════════════════════════════════════
           IMPORT — Đọc file và lưu vào DB
        ════════════════════════════════════ */
        public async Task<IActionResult> OnPostImportAsync(IFormFile file)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            TotalHolidays = await _context.Holidays.CountAsync();
            ShowResult = true;

            // Validate file
            if (file == null || file.Length == 0)
            {
                ImportErrors.Add("Dòng 0: Không có file được upload");
                ImportFailed++;
                return Page();
            }

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ImportErrors.Add("Dòng 0: File phải có định dạng .xlsx");
                ImportFailed++;
                return Page();
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                ImportErrors.Add("Dòng 0: File vượt quá giới hạn 5 MB");
                ImportFailed++;
                return Page();
            }

            // Tải danh sách ngày nghỉ hiện có để check trùng
            var existingKeys = (await _context.Holidays
                .Select(h => new { h.HolidayName, h.StartDate }) // Chỉ lấy những cột cần thiết để tối ưu
                .ToListAsync())
                .Select(h => h.HolidayName.ToLower() + "|" + h.StartDate.ToString()) // Xử lý chuỗi trên RAM
                .ToHashSet();

            try
            {
                using var stream = file.OpenReadStream();
                using var wb = new XLWorkbook(stream);

                var ws = wb.Worksheets.FirstOrDefault();
                if (ws == null)
                {
                    ImportErrors.Add("Dòng 0: File không có worksheet nào");
                    ImportFailed++;
                    return Page();
                }

                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

                // Đọc header để xác định cột
                var headerRow = ws.Row(1);
                var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c <= ws.LastColumnUsed()?.ColumnNumber(); c++)
                {
                    var h = ws.Cell(1, c).GetString().Trim();
                    if (!string.IsNullOrEmpty(h)) colMap[h] = c;
                }

                // Kiểm tra cột bắt buộc
                var required = new[] { "HolidayName", "StartDate", "EndDate" };
                var missing = required.Where(r => !colMap.ContainsKey(r)).ToList();
                if (missing.Any())
                {
                    ImportErrors.Add($"Dòng 0: Thiếu cột bắt buộc: {string.Join(", ", missing)}");
                    ImportFailed++;
                    return Page();
                }

                var toAdd = new List<Holiday>();

                for (int row = 2; row <= lastRow; row++)
                {
                    // Bỏ qua hàng trống
                    var checkCell = ws.Cell(row, colMap["HolidayName"]).GetString().Trim();
                    if (string.IsNullOrEmpty(checkCell)) continue;

                    string GetCol(string name) =>
                        colMap.TryGetValue(name, out var c) ? ws.Cell(row, c).GetString().Trim() : "";

                    var name = GetCol("HolidayName");
                    var startRaw = GetCol("StartDate");
                    var endRaw = GetCol("EndDate");
                    var description = GetCol("Description");

                    // Validate HolidayName
                    if (string.IsNullOrEmpty(name))
                    {
                        ImportErrors.Add($"Dòng {row}: HolidayName không được để trống");
                        ImportFailed++; continue;
                    }
                    if (name.Length > 200)
                    {
                        ImportErrors.Add($"Dòng {row}: HolidayName không được vượt quá 200 ký tự");
                        ImportFailed++; continue;
                    }

                    // Parse ngày
                    if (!TryParseDate(startRaw, out var startDate))
                    {
                        ImportErrors.Add($"Dòng {row}: StartDate '{startRaw}' không hợp lệ (dùng dd/MM/yyyy hoặc yyyy-MM-dd)");
                        ImportFailed++; continue;
                    }
                    if (!TryParseDate(endRaw, out var endDate))
                    {
                        ImportErrors.Add($"Dòng {row}: EndDate '{endRaw}' không hợp lệ (dùng dd/MM/yyyy hoặc yyyy-MM-dd)");
                        ImportFailed++; continue;
                    }
                    if (endDate < startDate)
                    {
                        ImportErrors.Add($"Dòng {row}: EndDate phải lớn hơn hoặc bằng StartDate");
                        ImportFailed++; continue;
                    }

                    // Kiểm tra trùng (tên + ngày bắt đầu)
                    var key = name.ToLower() + "|" + startDate.ToString();
                    if (existingKeys.Contains(key))
                    {
                        // Bỏ qua, không coi là lỗi
                        continue;
                    }

                    existingKeys.Add(key); // tránh trùng trong cùng file

                    toAdd.Add(new Holiday
                    {
                        HolidayName = name,
                        StartDate = startDate,
                        EndDate = endDate,
                        Description = string.IsNullOrEmpty(description) ? null : description,
                        CreatedAt = DateTime.Now
                    });
                }

                // Bulk save
                if (toAdd.Any())
                {
                    await _context.Holidays.AddRangeAsync(toAdd);
                    await _context.SaveChangesAsync();
                    ImportSuccess = toAdd.Count;
                }
            }
            catch (Exception ex)
            {
                ImportErrors.Add($"Dòng 0: Lỗi đọc file — {ex.Message}");
                ImportFailed++;
            }

            TotalHolidays = await _context.Holidays.CountAsync();

            if (ImportSuccess > 0)
                TempData["SuccessMessage"] = $"Import thành công {ImportSuccess} ngày nghỉ lễ!";

            return Page();
        }

        /* ════════════════════════════════════
           HELPER — Parse ngày
        ════════════════════════════════════ */
        private static bool TryParseDate(string raw, out DateOnly result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            // dd/MM/yyyy
            if (DateOnly.TryParseExact(raw, "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result))
                return true;

            // yyyy-MM-dd
            if (DateOnly.TryParseExact(raw, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result))
                return true;

            // Excel serial number (OADate)
            if (double.TryParse(raw, out var serial) && serial > 0)
            {
                var dt = DateTime.FromOADate(serial);
                result = DateOnly.FromDateTime(dt);
                return true;
            }

            return false;
        }
    }
}