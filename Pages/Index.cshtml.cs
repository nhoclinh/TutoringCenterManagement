using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace TutoringCenterManagement.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return RedirectToPage("/Account/Login");

            var role = User.FindFirstValue(ClaimTypes.Role);

            return role switch
            {
                "Admin" => RedirectToPage("/Admin/Dashboard"),
                "Teacher" => RedirectToPage("/Teacher/Schedule"),
                "Student" => RedirectToPage("/Student/Schedule"),
                "Parent" => RedirectToPage("/Parent/Dashboard"),
                _ => RedirectToPage("/Account/Login")
            };
        }
    }
}
