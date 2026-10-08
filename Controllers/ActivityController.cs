using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ActivityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public ActivityController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
        }

        private async Task<(string UserId, string UserName, string Role)> GetCurrentUserContextAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = user != null ? await _userManager.GetRolesAsync(user) : new string[0];
            var primaryRole = roles.Count > 0 ? roles[0] : "SalesExecutive";
            return (user?.Id ?? string.Empty, user?.FullName ?? user?.UserName ?? "User", primaryRole);
        }

        private async Task PopulateDropDownsAsync(int? selectedCustomerId = null, int? selectedLeadId = null, string? selectedUserId = null)
        {
            var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
            ViewBag.CustomersList = new SelectList(customers, "CustomerId", "CustomerName", selectedCustomerId);

            var leads = await _context.Leads.Where(l => l.Status != "Converted").OrderBy(l => l.LeadName).ToListAsync();
            ViewBag.LeadsList = new SelectList(leads, "LeadId", "LeadName", selectedLeadId);

            var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.UsersList = new SelectList(users, "Id", "FullName", selectedUserId);
        }

        // GET: /Activity
        public async Task<IActionResult> Index(string? type, string? status, string? assignedTo)
        {
            var ctx = await GetCurrentUserContextAsync();
            var query = _context.Activities
                .Include(a => a.Customer)
                .Include(a => a.Lead)
                .Include(a => a.AssignedUser)
                .AsQueryable();

            if (ctx.Role == "SalesExecutive" && !string.IsNullOrEmpty(ctx.UserId))
            {
                query = query.Where(a => a.AssignedTo == ctx.UserId);
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(a => a.ActivityType.ToLower() == type.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status.ToLower() == status.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(assignedTo))
            {
                query = query.Where(a => a.AssignedTo == assignedTo);
            }

            var activities = await query.OrderByDescending(a => a.ActivityDate).ToListAsync();

            ViewBag.CurrentType = type;
            ViewBag.CurrentStatus = status;
            await PopulateDropDownsAsync(null, null, assignedTo);

            return View(activities);
        }

        // GET: /Activity/Create
        public async Task<IActionResult> Create(int? customerId, int? leadId)
        {
            var ctx = await GetCurrentUserContextAsync();
            var model = new Activity
            {
                CustomerId = customerId,
                LeadId = leadId,
                ActivityType = "Call",
                Status = "Completed",
                ActivityDate = DateTime.UtcNow,
                AssignedTo = ctx.UserId
            };

            await PopulateDropDownsAsync(customerId, leadId, ctx.UserId);
            return View(model);
        }

        // POST: /Activity/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Activity activity)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropDownsAsync(activity.CustomerId, activity.LeadId, activity.AssignedTo);
                return View(activity);
            }

            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            activity.CreatedDate = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(activity.AssignedTo))
            {
                activity.AssignedTo = ctx.UserId;
            }

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                ctx.UserId,
                ctx.UserName,
                "Log Activity",
                "Activity",
                activity.ActivityId.ToString(),
                oldValue: null,
                newValue: new { activity.ActivityId, activity.ActivityType, activity.Subject, activity.ActivityDate },
                details: $"Activity '{activity.Subject}' ({activity.ActivityType}) logged.",
                ipAddress: ip);

            TempData["SuccessMessage"] = $"Activity '{activity.Subject}' recorded successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
