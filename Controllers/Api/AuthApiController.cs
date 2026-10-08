using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using AcxiomCRM.Models.DTOs;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public class AuthApiController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public AuthApiController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditService = auditService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var user = await _userManager.FindByEmailAsync(dto.Email) ?? await _userManager.FindByNameAsync(dto.Email);
            if (user == null || !user.IsActive)
            {
                await _auditService.LogAsync(null, dto.Email, "API Failed Login", "Auth API", null, details: "Invalid API credentials", ipAddress: ip, result: "Failed");
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid credentials or inactive user account." });
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                await _auditService.LogAsync(user.Id, user.UserName, "API Failed Login - Locked", "Auth API", user.Id, details: "Locked out", ipAddress: ip, result: "Locked");
                return StatusCode(423, new AuthResponseDto { Success = false, Message = "Account is locked due to excessive failed attempts." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                await _auditService.LogAsync(user.Id, user.UserName, "API Login", "Auth API", user.Id, details: "API authentication successful", ipAddress: ip, result: "Success");

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Authentication successful",
                    UserId = user.Id,
                    UserName = user.FullName ?? user.UserName,
                    Role = roles.FirstOrDefault() ?? "SalesExecutive"
                });
            }

            if (result.IsLockedOut)
            {
                return StatusCode(423, new AuthResponseDto { Success = false, Message = "Account locked out after repeated failed logins." });
            }

            return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid credentials." });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { success = true, message = "Session ended successfully." });
        }
    }
}
