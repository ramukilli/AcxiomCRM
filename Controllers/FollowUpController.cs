using System;
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
    public class FollowUpController : Controller
    {
        private readonly IFollowUpService _followUpService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public FollowUpController(
            IFollowUpService followUpService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _followUpService = followUpService;
            _userManager = userManager;
            _context = context;
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

        // GET: /FollowUp
        public async Task<IActionResult> Index(string? status, string? type)
        {
            var ctx = await GetCurrentUserContextAsync();
            var followUps = await _followUpService.GetAllAsync(status, type, ctx.UserId, ctx.Role);

            ViewBag.CurrentStatus = status;
            ViewBag.CurrentType = type;
            ViewBag.OverdueCount = (await _followUpService.GetOverdueAsync(ctx.UserId, ctx.Role)).Count;
            ViewBag.UpcomingCount = (await _followUpService.GetUpcomingAsync(ctx.UserId, ctx.Role)).Count;

            return View(followUps);
        }

        // GET: /FollowUp/Create
        public async Task<IActionResult> Create(int? customerId, int? leadId)
        {
            var ctx = await GetCurrentUserContextAsync();
            var model = new FollowUp
            {
                CustomerId = customerId,
                LeadId = leadId,
                RelatedType = customerId.HasValue ? "Customer" : (leadId.HasValue ? "Lead" : "Customer"),
                FollowUpDate = DateTime.UtcNow.AddDays(1).Date.AddHours(10), // default tomorrow at 10 AM
                FollowUpType = "Call",
                Status = "Planned",
                AssignedTo = ctx.UserId
            };

            await PopulateDropDownsAsync(customerId, leadId, ctx.UserId);
            return View(model);
        }

        // POST: /FollowUp/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FollowUp followUp)
        {
            // Business Validation: Follow-up date cannot be earlier than today for planned follow-up
            if (followUp.Status == "Planned" && followUp.FollowUpDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError(nameof(followUp.FollowUpDate), "Follow-up date cannot be earlier than today.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropDownsAsync(followUp.CustomerId, followUp.LeadId, followUp.AssignedTo);
                return View(followUp);
            }

            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error, created) = await _followUpService.CreateAsync(followUp, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to schedule follow-up.");
                await PopulateDropDownsAsync(followUp.CustomerId, followUp.LeadId, followUp.AssignedTo);
                return View(followUp);
            }

            TempData["SuccessMessage"] = $"Follow-up '{followUp.Subject}' scheduled for {followUp.FollowUpDate:yyyy-MM-dd HH:mm}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /FollowUp/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status, string? remarks)
        {
            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error) = await _followUpService.UpdateStatusAsync(id, status, remarks, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to update status.";
            }
            else
            {
                TempData["SuccessMessage"] = $"Follow-up marked as '{status}'.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /FollowUp/Reschedule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule(int id, DateTime newDate, string? remarks)
        {
            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error) = await _followUpService.RescheduleAsync(id, newDate, remarks, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to reschedule follow-up.";
            }
            else
            {
                TempData["SuccessMessage"] = $"Follow-up successfully rescheduled to {newDate:yyyy-MM-dd HH:mm}.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /FollowUp/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error) = await _followUpService.DeleteAsync(id, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to delete follow-up.";
            }
            else
            {
                TempData["SuccessMessage"] = "Follow-up removed.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
