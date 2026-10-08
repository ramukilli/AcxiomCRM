using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IAuditService _auditService;
        private readonly IConfiguration _configuration;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IAuditService auditService,
            IConfiguration configuration)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
            _configuration = configuration;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            ViewData["ReturnUrl"] = model.ReturnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            // Find user by Email or Username
            var user = await _userManager.FindByEmailAsync(model.Email) ?? await _userManager.FindByNameAsync(model.Email);
            if (user == null)
            {
                await _auditService.LogAsync(
                    userId: null,
                    userName: model.Email,
                    action: "Failed Login",
                    entityName: "Authentication",
                    recordId: null,
                    details: "Attempted login with non-existent account.",
                    ipAddress: ipAddress,
                    result: "Failed");

                ModelState.AddModelError(string.Empty, "Invalid login credentials.");
                return View(model);
            }

            // Check if user is active
            if (!user.IsActive)
            {
                await _auditService.LogAsync(
                    userId: user.Id,
                    userName: user.UserName,
                    action: "Failed Login",
                    entityName: "Authentication",
                    recordId: user.Id,
                    details: "Deactivated user attempted to sign in.",
                    ipAddress: ipAddress,
                    result: "Blocked");

                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact your system administrator.");
                return View(model);
            }

            // Check lockout
            if (await _userManager.IsLockedOutAsync(user))
            {
                await _auditService.LogAsync(
                    userId: user.Id,
                    userName: user.UserName,
                    action: "Failed Login - Locked Out",
                    entityName: "Authentication",
                    recordId: user.Id,
                    details: $"User attempted login while account is locked out until {user.LockoutEnd}.",
                    ipAddress: ipAddress,
                    result: "Locked");

                ModelState.AddModelError(string.Empty, "This account is temporarily locked out due to multiple failed login attempts. Please try again after 15 minutes.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await _auditService.LogAsync(
                    userId: user.Id,
                    userName: user.UserName,
                    action: "Login",
                    entityName: "Authentication",
                    recordId: user.Id,
                    details: $"User '{user.FullName}' ({user.Email}) successfully logged in.",
                    ipAddress: ipAddress,
                    result: "Success");

                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }
                return RedirectToAction("Index", "Dashboard");
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAsync(
                    userId: user.Id,
                    userName: user.UserName,
                    action: "Account Lockout",
                    entityName: "Security",
                    recordId: user.Id,
                    details: "Account locked automatically after maximum failed attempts reached.",
                    ipAddress: ipAddress,
                    result: "Locked");

                ModelState.AddModelError(string.Empty, "Account locked due to consecutive failed attempts. Please wait 15 minutes or contact your administrator.");
                return View(model);
            }

            // Failed login
            await _auditService.LogAsync(
                userId: user.Id,
                userName: user.UserName,
                action: "Failed Login",
                entityName: "Authentication",
                recordId: user.Id,
                details: "Incorrect password supplied.",
                ipAddress: ipAddress,
                result: "Failed");

            ModelState.AddModelError(string.Empty, "Invalid login credentials.");
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(nameof(model.Email), "A user with this email address already exists.");
                return View(model);
            }

            // Only allow valid roles
            var roleName = model.Role;
            if (roleName != "SalesExecutive" && roleName != "Manager" && roleName != "Admin")
            {
                roleName = "SalesExecutive";
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

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (createResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, roleName);

                await _auditService.LogAsync(
                    userId: user.Id,
                    userName: user.UserName,
                    action: "Register",
                    entityName: "User",
                    recordId: user.Id,
                    details: $"User registered: {user.FullName} with role {roleName}.",
                    ipAddress: ipAddress,
                    result: "Success");

                TempData["SuccessMessage"] = "Registration successful! You may now log in with your credentials.";
                return RedirectToAction(nameof(Login));
            }

            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            if (user != null)
            {
                await _auditService.LogAsync(
                    userId: user.Id,
                    userName: user.UserName,
                    action: "Logout",
                    entityName: "Authentication",
                    recordId: user.Id,
                    details: $"User '{user.FullName}' logged out.",
                    ipAddress: ipAddress,
                    result: "Success");
            }

            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ==========================================
        // GOOGLE OAUTH AUTHENTICATION WORKFLOW
        // ==========================================

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider = "Google", string? returnUrl = null)
        {
            var googleClientId = _configuration["Authentication:Google:ClientId"];
            // If live Google credentials are provided in appsettings.json, trigger real Google OAuth challenge
            if (!string.IsNullOrEmpty(googleClientId) && googleClientId != "YOUR_GOOGLE_CLIENT_ID")
            {
                var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
                var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
                return Challenge(properties, provider);
            }

            // Otherwise, route to interactive Google OAuth Demo/Connect screen
            return RedirectToAction(nameof(GoogleConnect), new { returnUrl });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult GoogleConnect(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GoogleDemoLogin(string email, string fullName, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                ModelState.AddModelError(string.Empty, "Please enter a valid Google account email.");
                ViewBag.ReturnUrl = returnUrl;
                return View(nameof(GoogleConnect));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = string.IsNullOrWhiteSpace(fullName) ? email.Split('@')[0] : fullName,
                    Department = "Google Workspace User",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                var createRes = await _userManager.CreateAsync(user);
                if (createRes.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "SalesExecutive");
                    await _auditService.LogAsync(
                        user.Id,
                        user.UserName,
                        "Google Register",
                        "Authentication",
                        user.Id,
                        details: $"User registered via Google OAuth: {user.FullName} ({user.Email}).",
                        ipAddress: ip,
                        result: "Success");
                }
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account has been deactivated.");
                ViewBag.ReturnUrl = returnUrl;
                return View(nameof(GoogleConnect));
            }

            await _signInManager.SignInAsync(user, isPersistent: true);
            await _auditService.LogAsync(
                user.Id,
                user.UserName,
                "Google Login",
                "Authentication",
                user.Id,
                details: $"User signed in via Google OAuth Single Sign-On: {user.FullName} ({user.Email}).",
                ipAddress: ip,
                result: "Success");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (remoteError != null)
            {
                TempData["ErrorMessage"] = $"Error from Google authentication: {remoteError}";
                return RedirectToAction(nameof(Login));
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                TempData["ErrorMessage"] = "Error loading external Google login profile.";
                return RedirectToAction(nameof(Login));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (signInResult.Succeeded)
            {
                await _auditService.LogAsync(null, info.Principal.Identity?.Name, "Google Login", "Authentication", null, details: "External Google OAuth sign in successful.", ipAddress: ip);
                return RedirectToAction("Index", "Dashboard");
            }

            var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var name = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? email;

            if (email != null)
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        FullName = name ?? "Google User",
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow
                    };
                    await _userManager.CreateAsync(user);
                    await _userManager.AddToRoleAsync(user, "SalesExecutive");
                }

                await _userManager.AddLoginAsync(user, info);
                await _signInManager.SignInAsync(user, isPersistent: false);
                await _auditService.LogAsync(user.Id, user.UserName, "Google Login", "Authentication", user.Id, details: "User authenticated with Google OAuth.", ipAddress: ip);

                return RedirectToAction("Index", "Dashboard");
            }

            TempData["ErrorMessage"] = "Could not retrieve email address from Google.";
            return RedirectToAction(nameof(Login));
        }
    }
}
