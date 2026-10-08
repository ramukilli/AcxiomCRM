using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserManagementController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IAuditService _auditService;

        public UserManagementController(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IAuditService auditService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
        }

        public class UserListItemViewModel
        {
            public string Id { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? Department { get; set; }
            public string Role { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public bool IsLockedOut { get; set; }
            public DateTimeOffset? LockoutEnd { get; set; }
            public int AccessFailedCount { get; set; }
            public DateTime CreatedDate { get; set; }
        }

        // GET: /UserManagement
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.OrderByDescending(u => u.CreatedDate).ToListAsync();
            var list = new List<UserListItemViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var isLockedOut = await _userManager.IsLockedOutAsync(user);

                list.Add(new UserListItemViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email ?? string.Empty,
                    Department = user.Department,
                    Role = roles.FirstOrDefault() ?? "None",
                    IsActive = user.IsActive,
                    IsLockedOut = isLockedOut,
                    LockoutEnd = user.LockoutEnd,
                    AccessFailedCount = user.AccessFailedCount,
                    CreatedDate = user.CreatedDate
                });
            }

            return View(list);
        }

        // GET: /UserManagement/Create
        public IActionResult Create()
        {
            return View(new RegisterViewModel());
        }

        // POST: /UserManagement/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError(nameof(model.Email), "User with this email already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Department = model.Department,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);

                var admin = await _userManager.GetUserAsync(User);
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                await _auditService.LogAsync(
                    admin?.Id,
                    admin?.UserName,
                    "Create User",
                    "User Management",
                    user.Id,
                    oldValue: null,
                    newValue: new { user.FullName, user.Email, model.Role, user.Department },
                    details: $"Administrator created user profile for '{user.FullName}' ({user.Email}) with role '{model.Role}'.",
                    ipAddress: ip);

                TempData["SuccessMessage"] = $"User '{user.FullName}' created successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var err in result.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            return View(model);
        }

        // POST: /UserManagement/ToggleStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var oldStatus = user.IsActive;
            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            var admin = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            await _auditService.LogAsync(
                admin?.Id,
                admin?.UserName,
                "Status Change",
                "User Management",
                user.Id,
                oldValue: new { IsActive = oldStatus },
                newValue: new { user.IsActive },
                details: $"User '{user.FullName}' was {(user.IsActive ? "activated" : "deactivated")}.",
                ipAddress: ip);

            TempData["SuccessMessage"] = $"User status updated to {(user.IsActive ? "Active" : "Deactivated")}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /UserManagement/Unlock
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            var admin = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            await _auditService.LogAsync(
                admin?.Id,
                admin?.UserName,
                "Unlock Account",
                "Security",
                user.Id,
                details: $"Administrator manually unlocked account for '{user.FullName}' ({user.Email}).",
                ipAddress: ip);

            TempData["SuccessMessage"] = $"Account '{user.Email}' has been unlocked.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /UserManagement/ChangeRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(string id, string newRole)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, newRole);

            var admin = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            await _auditService.LogAsync(
                admin?.Id,
                admin?.UserName,
                "Role Change",
                "Security",
                user.Id,
                oldValue: new { Roles = currentRoles },
                newValue: new { Role = newRole },
                details: $"User '{user.FullName}' role changed from [{string.Join(", ", currentRoles)}] to '{newRole}'.",
                ipAddress: ip);

            TempData["SuccessMessage"] = $"User role updated to '{newRole}'.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /UserManagement/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            var admin = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            if (result.Succeeded)
            {
                await _auditService.LogAsync(
                    admin?.Id,
                    admin?.UserName,
                    "Password Reset",
                    "Security",
                    user.Id,
                    details: $"Administrator executed password reset for '{user.FullName}' ({user.Email}).",
                    ipAddress: ip);

                TempData["SuccessMessage"] = $"Password for '{user.Email}' was reset successfully.";
            }
            else
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                TempData["ErrorMessage"] = $"Failed to reset password: {errors}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
