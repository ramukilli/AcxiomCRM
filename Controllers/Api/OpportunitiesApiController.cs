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
    [Route("api/opportunities")]
    [Produces("application/json")]
    public class OpportunitiesApiController : ControllerBase
    {
        private readonly IOpportunityService _oppService;
        private readonly UserManager<ApplicationUser> _userManager;

        public OpportunitiesApiController(
            IOpportunityService oppService,
            UserManager<ApplicationUser> userManager)
        {
            _oppService = oppService;
            _userManager = userManager;
        }

        private static OpportunityResponseDto MapToDto(Opportunity o) => new()
        {
            OpportunityId = o.OpportunityId,
            OpportunityName = o.OpportunityName,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer?.CustomerName,
            Amount = o.Amount,
            Stage = o.Stage,
            Probability = o.Probability,
            WeightedAmount = o.WeightedAmount,
            ExpectedCloseDate = o.ExpectedCloseDate,
            Status = o.Status,
            AssignedUserName = o.AssignedUser?.FullName ?? o.AssignedUser?.UserName,
            CreatedDate = o.CreatedDate
        };

        // GET: /api/opportunities
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OpportunityResponseDto>>> GetOpportunities([FromQuery] string? search, [FromQuery] string? stage)
        {
            var opps = await _oppService.GetAllAsync(search, stage);
            return Ok(opps.Select(MapToDto));
        }

        // GET: /api/opportunities/5
        [HttpGet("{id}")]
        public async Task<ActionResult<OpportunityResponseDto>> GetOpportunity(int id)
        {
            var opp = await _oppService.GetByIdAsync(id);
            if (opp == null)
            {
                return NotFound(new { message = $"Opportunity with ID {id} not found." });
            }
            return Ok(MapToDto(opp));
        }

        // POST: /api/opportunities
        [HttpPost]
        public async Task<ActionResult<OpportunityResponseDto>> CreateOpportunity([FromBody] CreateOpportunityDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var opp = new Opportunity
            {
                OpportunityName = dto.OpportunityName,
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                Amount = dto.Amount,
                Stage = dto.Stage,
                Probability = dto.Probability,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Source = dto.Source,
                Notes = dto.Notes,
                AssignedTo = dto.AssignedTo
            };

            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id ?? "api-user";
            var userName = user?.UserName ?? "REST-API";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error, created) = await _oppService.CreateAsync(opp, userId, userName, ip);
            if (!success || created == null)
            {
                return BadRequest(new { message = error ?? "Failed to create opportunity." });
            }

            var responseDto = MapToDto(created);
            return CreatedAtAction(nameof(GetOpportunity), new { id = created.OpportunityId }, responseDto);
        }
    }
}
