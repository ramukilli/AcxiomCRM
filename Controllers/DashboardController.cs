using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IDashboardService _dashboardService;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(
            IDashboardService dashboardService,
            UserManager<ApplicationUser> userManager)
        {
            _dashboardService = dashboardService;
            _userManager = userManager;
        }

        // GET: /Dashboard
        public async Task<IActionResult> Index(string? dateFilter)
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = user != null ? await _userManager.GetRolesAsync(user) : new string[0];
            var primaryRole = roles.Count > 0 ? roles[0] : "SalesExecutive";

            var model = await _dashboardService.GetDashboardMetricsAsync(user?.Id, primaryRole, dateFilter);

            ViewBag.DateFilter = dateFilter ?? "AllTime";
            ViewBag.UserRole = primaryRole;
            ViewBag.UserName = user?.FullName ?? user?.UserName ?? "User";

            return View(model);
        }
    }
}
