using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin.Users
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ApplicationDbContext context, ILogger<IndexModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public List<UserViewModel> Users { get; set; } = new List<UserViewModel>();

        [BindProperty(SupportsGet = true)]
        public string? SearchString { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? RoleFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Kiểm tra quyền Admin
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToPage("/Account/Login");
            }

            // Query users
            var query = _context.Accounts
                .Include(a => a.Staff)
                .Include(a => a.Teacher)
                .Include(a => a.Student)
                .Include(a => a.Parent)
                .AsQueryable();

            // Filter by role
            if (!string.IsNullOrEmpty(RoleFilter))
            {
                if (Enum.TryParse<Role>(RoleFilter, out var roleEnum))
                {
                    query = query.Where(a => a.Role == roleEnum);
                }
            }

            // Filter by status
            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (Enum.TryParse<IsActive>(StatusFilter, out var statusEnum))
                {
                    query = query.Where(a => a.IsActive == statusEnum);
                }
            }

            // Search by username or fullname
            if (!string.IsNullOrEmpty(SearchString))
            {
                var search = SearchString.ToLower();
                query = query.Where(a =>
                    a.Username.ToLower().Contains(search) ||
                    (a.Staff != null && a.Staff.Fullname.ToLower().Contains(search)) ||
                    (a.Teacher != null && a.Teacher.Fullname.ToLower().Contains(search)) ||
                    (a.Student != null && a.Student.Fullname.ToLower().Contains(search)) ||
                    (a.Parent != null && a.Parent.Fullname.ToLower().Contains(search))
                );
            }

            // Execute query and map to ViewModel
            var accounts = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

            Users = accounts.Select(a => new UserViewModel
            {
                AccountId = a.AccountId,
                Username = a.Username,
                Role = a.Role.ToString(),
                IsActive = a.IsActive.ToString(),
                CreatedAt = a.CreatedAt,
                Fullname = a.Role switch
                {
                    Role.Admin => a.Staff?.Fullname ?? "N/A",
                    Role.Teacher => a.Teacher?.Fullname ?? "N/A",
                    Role.Student => a.Student?.Fullname ?? "N/A",
                    Role.Parent => a.Parent?.Fullname ?? "N/A",
                    _ => "N/A"
                },
                Phone = a.Role switch
                {
                    Role.Admin => a.Staff?.Phone ?? "N/A",
                    Role.Teacher => a.Teacher?.Phone ?? "N/A",
                    Role.Student => a.Student?.Phone ?? "N/A",
                    Role.Parent => a.Parent?.Phone ?? "N/A",
                    _ => "N/A"
                }
            }).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int accountId, string action)
        {
            try
            {
                var account = await _context.Accounts.FindAsync(accountId);
                if (account == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy tài khoản!";
                    return RedirectToPage();
                }

                account.IsActive = action == "activate" ? IsActive.Active : IsActive.NotActive;
                await _context.SaveChangesAsync();

                var actionText = action == "activate" ? "kích hoạt" : "vô hiệu hóa";
                TempData["SuccessMessage"] = $"Đã {actionText} tài khoản {account.Username} thành công!";
                _logger.LogInformation("Account {Username} status changed to {Status}", account.Username, account.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling status for account {AccountId}", accountId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật trạng thái!";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int accountId)
        {
            try
            {
                var account = await _context.Accounts.FindAsync(accountId);
                if (account == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy tài khoản!";
                    return RedirectToPage();
                }

                _context.Accounts.Remove(account);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã xóa tài khoản {account.Username} thành công!";
                _logger.LogInformation("Account {Username} deleted", account.Username);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting account {AccountId}", accountId);
                TempData["ErrorMessage"] = "Không thể xóa tài khoản này vì có dữ liệu liên quan (Session, Attendance...)!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account {AccountId}", accountId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xóa tài khoản!";
            }

            return RedirectToPage();
        }

        public class UserViewModel
        {
            public int AccountId { get; set; }
            public string Username { get; set; } = string.Empty;
            public string Fullname { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string IsActive { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }
    }
}