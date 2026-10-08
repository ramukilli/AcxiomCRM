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
    public class OpportunityController : Controller
    {
        private readonly IOpportunityService _opportunityService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public OpportunityController(
            IOpportunityService opportunityService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _opportunityService = opportunityService;
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

        private async Task PopulateDropDownsAsync(int? selectedCustomerId = null, string? selectedUserId = null)
        {
            var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
            ViewBag.CustomersList = new SelectList(customers, "CustomerId", "CustomerName", selectedCustomerId);

            var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.UsersList = new SelectList(users, "Id", "FullName", selectedUserId);
        }

        // GET: /Opportunity
        public async Task<IActionResult> Index(string? search, string? stage, string? status, string? viewMode = "list")
        {
            var ctx = await GetCurrentUserContextAsync();
            var opps = await _opportunityService.GetAllAsync(search, stage, status, ctx.UserId, ctx.Role);

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStage = stage;
            ViewBag.CurrentStatus = status;
            ViewBag.ViewMode = viewMode;
            ViewBag.UserRole = ctx.Role;

            return View(opps);
        }

        // GET: /Opportunity/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var opp = await _opportunityService.GetByIdAsync(id);
            if (opp == null)
            {
                TempData["ErrorMessage"] = "Opportunity not found.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = await GetCurrentUserContextAsync();
            if (ctx.Role == "SalesExecutive" && opp.AssignedTo != ctx.UserId)
            {
                TempData["ErrorMessage"] = "Access Denied: You can only view opportunities assigned to you.";
                return RedirectToAction(nameof(Index));
            }

            return View(opp);
        }

        // GET: /Opportunity/Create
        public async Task<IActionResult> Create(int? customerId, int? leadId)
        {
            var ctx = await GetCurrentUserContextAsync();
            var opp = new Opportunity
            {
                CustomerId = customerId,
                LeadId = leadId,
                Stage = "Qualification",
                Probability = 20,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
                AssignedTo = ctx.UserId
            };

            await PopulateDropDownsAsync(customerId, ctx.UserId);
            return View(opp);
        }

        // POST: /Opportunity/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Opportunity opportunity)
        {
            // Business Rule Validation 1: Amount > 0
            if (opportunity.Amount <= 0)
            {
                ModelState.AddModelError(nameof(opportunity.Amount), "Opportunity Amount must be greater than 0.");
            }

            // Business Rule Validation 2: Probability 0-100
            if (opportunity.Probability < 0 || opportunity.Probability > 100)
            {
                ModelState.AddModelError(nameof(opportunity.Probability), "Probability must be between 0 and 100.");
            }

            // Business Rule Validation 3: Expected Close Date cannot be in the past
            if (opportunity.Status != "Won" && opportunity.Status != "Lost" && opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError(nameof(opportunity.ExpectedCloseDate), "Expected Close Date cannot be in the past.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropDownsAsync(opportunity.CustomerId, opportunity.AssignedTo);
                return View(opportunity);
            }

            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error, createdOpp) = await _opportunityService.CreateAsync(opportunity, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to save opportunity.");
                await PopulateDropDownsAsync(opportunity.CustomerId, opportunity.AssignedTo);
                return View(opportunity);
            }

            TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Opportunity/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var opp = await _opportunityService.GetByIdAsync(id);
            if (opp == null)
            {
                TempData["ErrorMessage"] = "Opportunity not found.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = await GetCurrentUserContextAsync();
            if (ctx.Role == "SalesExecutive" && opp.AssignedTo != ctx.UserId)
            {
                TempData["ErrorMessage"] = "Access Denied: You can only edit opportunities assigned to you.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropDownsAsync(opp.CustomerId, opp.AssignedTo);
            return View(opp);
        }

        // POST: /Opportunity/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Opportunity opportunity)
        {
            if (id != opportunity.OpportunityId)
            {
                return NotFound();
            }

            if (opportunity.Amount <= 0)
            {
                ModelState.AddModelError(nameof(opportunity.Amount), "Opportunity Amount must be greater than 0.");
            }

            if (opportunity.Probability < 0 || opportunity.Probability > 100)
            {
                ModelState.AddModelError(nameof(opportunity.Probability), "Probability must be between 0 and 100.");
            }

            if (opportunity.Stage != "Won" && opportunity.Stage != "Lost" && opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError(nameof(opportunity.ExpectedCloseDate), "Expected Close Date cannot be in the past.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropDownsAsync(opportunity.CustomerId, opportunity.AssignedTo);
                return View(opportunity);
            }

            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error) = await _opportunityService.UpdateAsync(opportunity, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to update opportunity.");
                await PopulateDropDownsAsync(opportunity.CustomerId, opportunity.AssignedTo);
                return View(opportunity);
            }

            TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Opportunity/SetStage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStage(int id, string newStage)
        {
            var opp = await _opportunityService.GetByIdAsync(id);
            if (opp == null)
            {
                return NotFound();
            }

            opp.Stage = newStage;
            if (newStage == "Won")
            {
                opp.Status = "Won";
                opp.Probability = 100;
            }
            else if (newStage == "Lost")
            {
                opp.Status = "Lost";
                opp.Probability = 0;
            }

            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error) = await _opportunityService.UpdateAsync(opp, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to update stage.";
            }
            else
            {
                TempData["SuccessMessage"] = $"Deal stage updated to '{newStage}'.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Opportunity/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error) = await _opportunityService.DeleteAsync(id, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Could not delete opportunity.";
            }
            else
            {
                TempData["SuccessMessage"] = "Opportunity record deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
