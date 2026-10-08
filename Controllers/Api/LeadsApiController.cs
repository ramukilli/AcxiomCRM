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
    [Route("api/leads")]
    [Produces("application/json")]
    public class LeadsApiController : ControllerBase
    {
        private readonly ILeadService _leadService;
        private readonly UserManager<ApplicationUser> _userManager;

        public LeadsApiController(
            ILeadService leadService,
            UserManager<ApplicationUser> userManager)
        {
            _leadService = leadService;
            _userManager = userManager;
        }

        private static LeadResponseDto MapToDto(Lead l) => new()
        {
            LeadId = l.LeadId,
            LeadCode = l.LeadCode,
            LeadName = l.LeadName,
            Email = l.Email,
            Phone = l.Phone,
            CompanyName = l.CompanyName,
            Source = l.Source,
            Status = l.Status,
            Priority = l.Priority,
            ExpectedValue = l.ExpectedValue,
            CreatedDate = l.CreatedDate,
            AssignedUserName = l.AssignedUser?.FullName ?? l.AssignedUser?.UserName,
            ConvertedCustomerId = l.ConvertedCustomerId
        };

        // GET: /api/leads
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LeadResponseDto>>> GetLeads([FromQuery] string? search, [FromQuery] string? status)
        {
            var leads = await _leadService.GetAllAsync(search, status);
            return Ok(leads.Select(MapToDto));
        }

        // GET: /api/leads/5
        [HttpGet("{id}")]
        public async Task<ActionResult<LeadResponseDto>> GetLead(int id)
        {
            var lead = await _leadService.GetByIdAsync(id);
            if (lead == null)
            {
                return NotFound(new { message = $"Lead with ID {id} not found." });
            }
            return Ok(MapToDto(lead));
        }

        // POST: /api/leads
        [HttpPost]
        public async Task<ActionResult<LeadResponseDto>> CreateLead([FromBody] CreateLeadDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var lead = new Lead
            {
                LeadName = dto.LeadName,
                Email = dto.Email,
                Phone = dto.Phone,
                CompanyName = dto.CompanyName,
                Source = dto.Source,
                Status = dto.Status,
                Priority = dto.Priority,
                ExpectedValue = dto.ExpectedValue,
                Notes = dto.Notes,
                AssignedTo = dto.AssignedTo
            };

            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id ?? "api-user";
            var userName = user?.UserName ?? "REST-API";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error, created) = await _leadService.CreateAsync(lead, userId, userName, ip);
            if (!success || created == null)
            {
                return BadRequest(new { message = error ?? "Failed to create lead." });
            }

            var responseDto = MapToDto(created);
            return CreatedAtAction(nameof(GetLead), new { id = created.LeadId }, responseDto);
        }
    }
}
