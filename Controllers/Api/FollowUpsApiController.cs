using System.Collections.Generic;
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
    [Route("api/followups")]
    [Produces("application/json")]
    public class FollowUpsApiController : ControllerBase
    {
        private readonly IFollowUpService _followUpService;
        private readonly UserManager<ApplicationUser> _userManager;

        public FollowUpsApiController(
            IFollowUpService followUpService,
            UserManager<ApplicationUser> userManager)
        {
            _followUpService = followUpService;
            _userManager = userManager;
        }

        private static FollowUpResponseDto MapToDto(FollowUp f) => new()
        {
            FollowUpId = f.FollowUpId,
            RelatedType = f.RelatedType,
            CustomerId = f.CustomerId,
            CustomerName = f.Customer?.CustomerName,
            LeadId = f.LeadId,
            LeadName = f.Lead?.LeadName,
            FollowUpDate = f.FollowUpDate,
            FollowUpType = f.FollowUpType,
            Subject = f.Subject,
            Status = f.Status,
            Remarks = f.Remarks,
            AssignedUserName = f.AssignedUser?.FullName ?? f.AssignedUser?.UserName
        };

        // GET: /api/followups
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FollowUpResponseDto>>> GetFollowUps([FromQuery] string? status, [FromQuery] string? type)
        {
            var followUps = await _followUpService.GetAllAsync(status, type);
            return Ok(followUps.Select(MapToDto));
        }

        // POST: /api/followups
        [HttpPost]
        public async Task<ActionResult<FollowUpResponseDto>> CreateFollowUp([FromBody] CreateFollowUpDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var followUp = new FollowUp
            {
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                RelatedType = dto.CustomerId.HasValue ? "Customer" : (dto.LeadId.HasValue ? "Lead" : "General"),
                FollowUpDate = dto.FollowUpDate,
                FollowUpType = dto.FollowUpType,
                Subject = dto.Subject,
                Status = dto.Status,
                Remarks = dto.Remarks,
                AssignedTo = dto.AssignedTo
            };

            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id ?? "api-user";
            var userName = user?.UserName ?? "REST-API";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error, created) = await _followUpService.CreateAsync(followUp, userId, userName, ip);
            if (!success || created == null)
            {
                return BadRequest(new { message = error ?? "Failed to create follow-up." });
            }

            return Ok(MapToDto(created));
        }
    }
}
