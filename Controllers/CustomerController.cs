using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomerController(
            ICustomerService customerService,
            UserManager<ApplicationUser> userManager)
        {
            _customerService = customerService;
            _userManager = userManager;
        }

        private async Task<(string UserId, string UserName, string Role)> GetCurrentUserContextAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = user != null ? await _userManager.GetRolesAsync(user) : new string[0];
            var primaryRole = roles.Count > 0 ? roles[0] : "SalesExecutive";
            return (user?.Id ?? string.Empty, user?.FullName ?? user?.UserName ?? "User", primaryRole);
        }

        // GET: /Customer
        public async Task<IActionResult> Index(string? search, string? status)
        {
            var ctx = await GetCurrentUserContextAsync();
            var customers = await _customerService.GetAllAsync(search, status, ctx.UserId, ctx.Role);

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;
            ViewBag.UserRole = ctx.Role;

            return View(customers);
        }

        // GET: /Customer/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null)
            {
                TempData["ErrorMessage"] = "Customer not found.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = await GetCurrentUserContextAsync();
            if (ctx.Role == "SalesExecutive" && customer.CreatedBy != ctx.UserId)
            {
                TempData["ErrorMessage"] = "Access Denied: You can only view customers assigned to you.";
                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }

        // GET: /Customer/Create
        public async Task<IActionResult> Create()
        {
            var code = await _customerService.GenerateCustomerCodeAsync();
            var model = new Customer
            {
                CustomerCode = code,
                Status = "Active"
            };
            return View(model);
        }

        // POST: /Customer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            // Business Rule: Validate email and phone format
            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            // Server-side duplicate validation
            var (success, error, createdCustomer) = await _customerService.CreateAsync(customer, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to create customer.");
                return View(customer);
            }

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' ({customer.CustomerCode}) created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Customer/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null)
            {
                TempData["ErrorMessage"] = "Customer not found.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = await GetCurrentUserContextAsync();
            if (ctx.Role == "SalesExecutive" && customer.CreatedBy != ctx.UserId)
            {
                TempData["ErrorMessage"] = "Access Denied: You can only edit customers assigned to you.";
                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }

        // POST: /Customer/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.CustomerId)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error) = await _customerService.UpdateAsync(customer, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to update customer.");
                return View(customer);
            }

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Customer/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var ctx = await GetCurrentUserContextAsync();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error) = await _customerService.DeleteAsync(id, ctx.UserId, ctx.UserName, ip);
            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Could not delete customer.";
            }
            else
            {
                TempData["SuccessMessage"] = "Customer record deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Remote validation endpoint for Email
        [HttpGet]
        public async Task<IActionResult> VerifyEmail(string email, int? customerId)
        {
            var isUnique = await _customerService.IsEmailUniqueAsync(email, customerId);
            return Json(isUnique);
        }

        // Remote validation endpoint for Phone
        [HttpGet]
        public async Task<IActionResult> VerifyPhone(string phone, int? customerId)
        {
            var isUnique = await _customerService.IsPhoneUniqueAsync(phone, customerId);
            return Json(isUnique);
        }
    }
}
