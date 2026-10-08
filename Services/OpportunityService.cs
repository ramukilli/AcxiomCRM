using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public class OpportunityService : IOpportunityService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        private static readonly HashSet<string> ValidStages = new(StringComparer.OrdinalIgnoreCase)
        {
            "Qualification", "Proposal", "Negotiation", "Won", "Lost"
        };

        public OpportunityService(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<List<Opportunity>> GetAllAsync(string? search = null, string? stage = null, string? status = null, string? currentUserId = null, string? role = null)
        {
            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Lead)
                .Include(o => o.AssignedUser)
                .AsQueryable();

            if (role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId))
            {
                query = query.Where(o => o.AssignedTo == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(o =>
                    o.OpportunityName.ToLower().Contains(s) ||
                    (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(s)) ||
                    o.Stage.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(stage))
            {
                query = query.Where(o => o.Stage.ToLower() == stage.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.Status.ToLower() == status.ToLower());
            }

            return await query.OrderByDescending(o => o.CreatedDate).ToListAsync();
        }

        public async Task<Opportunity?> GetByIdAsync(int id)
        {
            return await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Lead)
                .Include(o => o.AssignedUser)
                .FirstOrDefaultAsync(o => o.OpportunityId == id);
        }

        public async Task<(bool Success, string? Error, Opportunity? Opportunity)> CreateAsync(
            Opportunity opportunity,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            // Business Validation 1: Amount must be greater than 0
            if (opportunity.Amount <= 0)
            {
                return (false, "Opportunity Amount must be greater than 0.", null);
            }

            // Business Validation 2: Probability 0 - 100
            if (opportunity.Probability < 0 || opportunity.Probability > 100)
            {
                return (false, "Probability must be between 0 and 100.", null);
            }

            // Business Validation 3: Expected Close Date cannot be in the past for active opportunities
            if (opportunity.Status != "Won" && opportunity.Status != "Lost" && opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                return (false, "Expected Close Date cannot be in the past.", null);
            }

            if (!ValidStages.Contains(opportunity.Stage))
            {
                return (false, $"Invalid pipeline stage '{opportunity.Stage}'.", null);
            }

            // Align Status with Stage
            if (opportunity.Stage == "Won")
            {
                opportunity.Status = "Won";
                opportunity.Probability = 100;
            }
            else if (opportunity.Stage == "Lost")
            {
                opportunity.Status = "Lost";
                opportunity.Probability = 0;
            }
            else
            {
                opportunity.Status = "Open";
            }

            opportunity.CreatedDate = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(opportunity.AssignedTo))
            {
                opportunity.AssignedTo = currentUserId;
            }

            _context.Opportunities.Add(opportunity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Create",
                "Opportunity",
                opportunity.OpportunityId.ToString(),
                oldValue: null,
                newValue: new { opportunity.OpportunityId, opportunity.OpportunityName, opportunity.Amount, opportunity.Stage, opportunity.Probability, opportunity.ExpectedCloseDate },
                details: $"Opportunity '{opportunity.OpportunityName}' (${opportunity.Amount:N2}) created.",
                ipAddress: ipAddress);

            return (true, null, opportunity);
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(
            Opportunity opportunity,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            var existing = await _context.Opportunities.FindAsync(opportunity.OpportunityId);
            if (existing == null)
            {
                return (false, "Opportunity not found.");
            }

            if (opportunity.Amount <= 0)
            {
                return (false, "Opportunity Amount must be greater than 0.");
            }

            if (opportunity.Probability < 0 || opportunity.Probability > 100)
            {
                return (false, "Probability must be between 0 and 100.");
            }

            if (!ValidStages.Contains(opportunity.Stage))
            {
                return (false, $"Invalid pipeline stage '{opportunity.Stage}'.");
            }

            if (opportunity.Stage == "Won")
            {
                opportunity.Status = "Won";
                opportunity.Probability = 100;
            }
            else if (opportunity.Stage == "Lost")
            {
                opportunity.Status = "Lost";
                opportunity.Probability = 0;
            }
            else
            {
                opportunity.Status = "Open";
                if (opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
                {
                    return (false, "Expected Close Date cannot be in the past.");
                }
            }

            var oldSnapshot = new { existing.OpportunityName, existing.Amount, existing.Stage, existing.Probability, existing.ExpectedCloseDate, existing.Status };

            existing.OpportunityName = opportunity.OpportunityName;
            existing.CustomerId = opportunity.CustomerId;
            existing.LeadId = opportunity.LeadId;
            existing.Amount = opportunity.Amount;
            existing.Stage = opportunity.Stage;
            existing.Probability = opportunity.Probability;
            existing.ExpectedCloseDate = opportunity.ExpectedCloseDate;
            existing.Status = opportunity.Status;
            existing.Source = opportunity.Source;
            existing.Notes = opportunity.Notes;
            existing.AssignedTo = opportunity.AssignedTo;

            await _context.SaveChangesAsync();

            var newSnapshot = new { existing.OpportunityName, existing.Amount, existing.Stage, existing.Probability, existing.ExpectedCloseDate, existing.Status };

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Update",
                "Opportunity",
                existing.OpportunityId.ToString(),
                oldValue: oldSnapshot,
                newValue: newSnapshot,
                details: $"Opportunity '{existing.OpportunityName}' updated.",
                ipAddress: ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> DeleteAsync(
            int id,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            var opportunity = await _context.Opportunities.FindAsync(id);
            if (opportunity == null)
            {
                return (false, "Opportunity not found.");
            }

            _context.Opportunities.Remove(opportunity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Delete",
                "Opportunity",
                id.ToString(),
                oldValue: new { opportunity.OpportunityId, opportunity.OpportunityName, opportunity.Amount },
                newValue: null,
                details: $"Opportunity '{opportunity.OpportunityName}' deleted.",
                ipAddress: ipAddress);

            return (true, null);
        }
    }
}
