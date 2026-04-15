using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;

namespace TutoringCenterManagement.Pages.Admin.Rooms
{
    public class ImportExportModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ImportExportModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public bool ShowResult { get; set; }
        public int ImportSuccess { get; set; }
        public int ImportFailed { get; set; }
        public List<string> ImportErrors { get; set; } = new();
        public int TotalRooms { get; set; }

        /* ════════════════════════════════════
           GET
        ════════════════════════════════════ */
        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            TotalRooms = await _context.Rooms.CountAsync();
            return Page();
        }

        /* ════════════════════════════════════
           EXPORT
        ════════════════════════════════════ */
        public async Task<IActionResult> OnGetExportAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var rooms = await _context.Rooms.OrderBy(r => r.RoomCode).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Rooms");

            // Header
            string[] headers = { "RoomCode", "RoomName", "Capacity", "Note" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2d3a5c");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Cột bắt buộc highlight vàng
            foreach (int req in new[] { 1, 2, 3 })
                ws.Cell(1, req).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");

            // Dữ liệu
            int row = 2;
            foreach (var r in rooms)
            {
                ws.Cell(row, 1).Value = r.RoomCode;
                ws.Cell(row, 2).Value = r.RoomName;
                ws.Cell(row, 3).Value = r.Capacity;
                ws.Cell(row, 4).Value = r.Note ?? "";

                if (row % 2 == 0)
                    ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fc");

                row++;
            }

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();
            ws.Column(1).Width = Math.Max(ws.Column(1).Width, 15);
            ws.Column(2).Width = Math.Max(ws.Column(2).Width, 25);
            ws.Column(4).Width = Math.Max(ws.Column(4).Width, 35);

            if (row > 2)
            {
                var rng = ws.Range(1, 1, row - 1, 4);
                rng.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rng.Style.Border.OutsideBorderColor = XLColor.FromHtml("#d0d5e8");
                rng.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                rng.Style.Border.InsideBorderColor = XLColor.FromHtml("#e8eaf2");
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Rooms_Export_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        /* ════════════════════════════════════
           TEMPLATE
        ════════════════════════════════════ */
        public IActionResult OnGetTemplateAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Rooms");

            var cols = new[]
            {
                ("RoomCode", true,  "Mã phòng, tối đa 50 ký tự (vd: P101)"),
                ("RoomName", true,  "Tên phòng, tối đa 100 ký tự"),
                ("Capacity", true,  "Sức chứa — số nguyên dương"),
                ("Note",     false, "Ghi chú thêm (tuỳ chọn)"),
            };

            for (int c = 0; c < cols.Length; c++)
            {
                var (name, req, hint) = cols[c];
                var cell = ws.Cell(1, c + 1);
                cell.Value = name;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = req
                    ? XLColor.FromHtml("#FFF2CC")
                    : XLColor.FromHtml("#f0f4ff");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var note = cell.CreateComment();
                note.AddText(hint);
            }

            ws.SheetView.FreezeRows(1);
            ws.Column(1).Width = 18;
            ws.Column(2).Width = 28;
            ws.Column(3).Width = 15;
            ws.Column(4).Width = 40;

            var hdr = ws.Range(1, 1, 1, 4);
            hdr.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            hdr.Style.Border.OutsideBorderColor = XLColor.FromHtml("#b0b8d0");
            hdr.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            hdr.Style.Border.InsideBorderColor = XLColor.FromHtml("#d0d5e8");

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Rooms_Template.xlsx");
        }

        /* ════════════════════════════════════
           IMPORT
        ════════════════════════════════════ */
        public async Task<IActionResult> OnPostImportAsync(IFormFile file)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            TotalRooms = await _context.Rooms.CountAsync();
            ShowResult = true;

            // Validate file
            if (file == null || file.Length == 0)
            {
                ImportErrors.Add("Dòng 0: Không có file được upload");
                ImportFailed++; return Page();
            }
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ImportErrors.Add("Dòng 0: File phải có định dạng .xlsx");
                ImportFailed++; return Page();
            }
            if (file.Length > 5 * 1024 * 1024)
            {
                ImportErrors.Add("Dòng 0: File vượt quá giới hạn 5 MB");
                ImportFailed++; return Page();
            }

            // Tải RoomCode đã tồn tại
            var existingCodes = (await _context.Rooms
                .Select(r => r.RoomCode.ToLower())
                .ToListAsync())
                .ToHashSet();

            try
            {
                using var stream = file.OpenReadStream();
                using var wb = new XLWorkbook(stream);

                var ws = wb.Worksheets.FirstOrDefault();
                if (ws == null)
                {
                    ImportErrors.Add("Dòng 0: File không có worksheet");
                    ImportFailed++; return Page();
                }

                int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

                // Map header → cột
                var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c <= (ws.LastColumnUsed()?.ColumnNumber() ?? 0); c++)
                {
                    var h = ws.Cell(1, c).GetString().Trim();
                    if (!string.IsNullOrEmpty(h)) colMap[h] = c;
                }

                var required = new[] { "RoomCode", "RoomName", "Capacity" };
                var missing = required.Where(r => !colMap.ContainsKey(r)).ToList();
                if (missing.Any())
                {
                    ImportErrors.Add($"Dòng 0: Thiếu cột bắt buộc: {string.Join(", ", missing)}");
                    ImportFailed++; return Page();
                }

                var toAdd = new List<Room>();

                for (int row = 2; row <= lastRow; row++)
                {
                    var checkCell = ws.Cell(row, colMap["RoomCode"]).GetString().Trim();
                    if (string.IsNullOrEmpty(checkCell)) continue;

                    string GetCol(string name) =>
                        colMap.TryGetValue(name, out var c) ? ws.Cell(row, c).GetString().Trim() : "";

                    var code = GetCol("RoomCode");
                    var name = GetCol("RoomName");
                    var capRaw = GetCol("Capacity");
                    var note = GetCol("Note");

                    // Validate RoomCode
                    if (string.IsNullOrEmpty(code))
                    {
                        ImportErrors.Add($"Dòng {row}: RoomCode không được để trống");
                        ImportFailed++; continue;
                    }
                    if (code.Length > 50)
                    {
                        ImportErrors.Add($"Dòng {row}: RoomCode '{code}' vượt quá 50 ký tự");
                        ImportFailed++; continue;
                    }

                    // Validate RoomName
                    if (string.IsNullOrEmpty(name))
                    {
                        ImportErrors.Add($"Dòng {row}: RoomName không được để trống");
                        ImportFailed++; continue;
                    }
                    if (name.Length > 100)
                    {
                        ImportErrors.Add($"Dòng {row}: RoomName '{name}' vượt quá 100 ký tự");
                        ImportFailed++; continue;
                    }

                    // Validate Capacity
                    if (!int.TryParse(capRaw, out var capacity) || capacity <= 0)
                    {
                        ImportErrors.Add($"Dòng {row}: Capacity '{capRaw}' không hợp lệ (phải là số nguyên dương)");
                        ImportFailed++; continue;
                    }

                    // Trùng RoomCode → bỏ qua (không lỗi)
                    if (existingCodes.Contains(code.ToLower())) continue;

                    existingCodes.Add(code.ToLower()); // tránh trùng trong cùng file

                    toAdd.Add(new Room
                    {
                        RoomCode = code,
                        RoomName = name,
                        Capacity = capacity,
                        Note = string.IsNullOrEmpty(note) ? null : note
                    });
                }

                if (toAdd.Any())
                {
                    await _context.Rooms.AddRangeAsync(toAdd);
                    await _context.SaveChangesAsync();
                    ImportSuccess = toAdd.Count;
                }
            }
            catch (Exception ex)
            {
                ImportErrors.Add($"Dòng 0: Lỗi đọc file — {ex.Message}");
                ImportFailed++;
            }

            TotalRooms = await _context.Rooms.CountAsync();

            if (ImportSuccess > 0)
                TempData["SuccessMessage"] = $"Import thành công {ImportSuccess} phòng học!";

            return Page();
        }
    }
}