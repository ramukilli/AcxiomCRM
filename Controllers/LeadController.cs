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
    public class LeadController : Controller
    {
        private readonly ILeadService _leadService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public LeadController(
            ILeadService leadService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _leadService = leadService;
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

        private async Task PopulateUsersDropDownListAsync(string? selectedUserId = null)
        {
            var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.UsersList = new SelectList(users, "Id", "FullName", selectedUserId);
        }

        // GET: /Lead
        public async Task<IActionResult> Index(string? search, string? status, string? assignedTo)
        {
            var ctx = await GetCurrentUserContextAsync();
            var leads = await _leadService.GetAllAsync(search, status, assignedTo, ctx.UserId, ctx.Role);

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentAssignedTo = assignedTo;
            ViewBag.UserRole = ctx.Role;
            await PopulateUsersDropDownListAsync(assignedTo);

            return View(leads);
        }

        // GET: /Lead/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var lead = await _leadService.GetByIdAsync(id);
            if (lead == null)
            {
                TempData["ErrorMessage"] = "Lead not found.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = await GetCurrentUserContextAsync();
            if (ctx.Role == "SalesExecutive" && lead.AssignedTo != ctx.UserId)
            {
                TempData["ErrorMessage"] = "Access Denied: You can only view leads assigned to you.";
                return RedirectToAction(nameof(Index));
            }

            return View(lead);
        }

        // GET: /Lead/Create
        public async Task<IActionResult> Create()
        {
            var code = await _leadService.GenerateLeadCodeAsync();
            var ctx = await GetCurrentUserContextAsync();

            var lead = new Lead
            {
                LeadCode = code,
                Status = "New",
                Priority = "Medium",
                Source = "Website",
                AssignedTo = ctx.UserId
            };

            await PopulateUsersDropDownListAsync(ctx.UserId);
            return View(lead);
        }

        // POST: /Lead/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Lead lead)
        {
            if (!ModelState.IsValid)
            {
                await PopulateUsersDropDownListAsync(lead.AssignedTo);
                return View(lead);
            }

            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error, createdLead) = await _leadService.CreateAsync(lead, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to create lead.");
                await PopulateUsersDropDownListAsync(lead.AssignedTo);
                return View(lead);
            }

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' ({lead.LeadCode}) created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Lead/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var lead = await _leadService.GetByIdAsync(id);
            if (lead == null)
            {
                TempData["ErrorMessage"] = "Lead not found.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = await GetCurrentUserContextAsync();
            if (ctx.Role == "SalesExecutive" && lead.AssignedTo != ctx.UserId)
            {
                TempData["ErrorMessage"] = "Access Denied: You can only edit leads assigned to you.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateUsersDropDownListAsync(lead.AssignedTo);
            return View(lead);
        }

        // POST: /Lead/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Lead lead)
        {
            if (id != lead.LeadId)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                await PopulateUsersDropDownListAsync(lead.AssignedTo);
                return View(lead);
            }

            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error) = await _leadService.UpdateAsync(lead, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to update lead.");
                await PopulateUsersDropDownListAsync(lead.AssignedTo);
                return View(lead);
            }

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Lead/Convert/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Convert(int id)
        {
            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error, customerId, oppId) = await _leadService.ConvertLeadAsync(id, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to convert lead.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["SuccessMessage"] = $"Lead successfully converted into Customer #{customerId}" + 
                (oppId.HasValue ? $" and new Opportunity #{oppId.Value}!" : "!");

            return RedirectToAction("Details", "Customer", new { id = customerId });
        }

        // POST: /Lead/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error) = await _leadService.DeleteAsync(id, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Could not delete lead.";
            }
            else
            {
                TempData["SuccessMessage"] = "Lead record removed successfully.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
