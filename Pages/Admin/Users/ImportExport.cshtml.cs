using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TutoringCenterManagement.Services.Implementations;

namespace TutoringCenterManagement.Pages.Admin.Users
{
    public class ImportExportModel : PageModel
    {
        private readonly UserImportExportService _service;

        public ImportExportModel(UserImportExportService service)
        {
            _service = service;
        }

        // Kết quả import sau khi POST
        public int ImportSuccess { get; set; }
        public int ImportFailed { get; set; }
        public List<string> ImportErrors { get; set; } = new();
        public bool ShowResult { get; set; } = false;

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            return Page();
        }

        // ── Export toàn bộ users ───────────────────────────────────────────
        public async Task<IActionResult> OnGetExportAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var bytes = await _service.ExportAllUsersAsync();
            var fileName = $"Users_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ── Download file template trống ──────────────────────────────────
        public IActionResult OnGetTemplateAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var bytes = _service.GenerateImportTemplate();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "ImportTemplate_Users.xlsx");
        }

        // ── Import từ file Excel ───────────────────────────────────────────
        public async Task<IActionResult> OnPostImportAsync(IFormFile file)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn file Excel!");
                return Page();
            }

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".xlsx")
            {
                ModelState.AddModelError("", "Chỉ hỗ trợ file .xlsx!");
                return Page();
            }

            if (file.Length > 5 * 1024 * 1024) // 5MB
            {
                ModelState.AddModelError("", "File quá lớn! Tối đa 5MB.");
                return Page();
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _service.ImportUsersAsync(stream);

                ImportSuccess = result.Success;
                ImportFailed = result.Failed;
                ImportErrors = result.Errors;
                ShowResult = true;

                if (result.Success > 0)
                    TempData["SuccessMessage"] = $"Import thành công {result.Success} người dùng!";

                if (result.Failed > 0)
                    TempData["ErrorMessage"] = $"Có {result.Failed} dòng lỗi, vui lòng kiểm tra bên dưới.";
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi xử lý file: {ex.Message}");
            }

            return Page();
        }
    }
}