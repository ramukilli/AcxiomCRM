using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public class LeadService : ILeadService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly ICustomerService _customerService;

        private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost"
        };

        public LeadService(ApplicationDbContext context, IAuditService auditService, ICustomerService customerService)
        {
            _context = context;
            _auditService = auditService;
            _customerService = customerService;
        }

        public async Task<List<Lead>> GetAllAsync(string? search = null, string? status = null, string? assignedTo = null, string? currentUserId = null, string? role = null)
        {
            var query = _context.Leads
                .Include(l => l.AssignedUser)
                .Include(l => l.ConvertedCustomer)
                .AsQueryable();

            if (role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId))
            {
                query = query.Where(l => l.AssignedTo == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(l =>
                    l.LeadName.ToLower().Contains(s) ||
                    l.Email.ToLower().Contains(s) ||
                    l.Phone.Contains(s) ||
                    (l.CompanyName != null && l.CompanyName.ToLower().Contains(s)) ||
                    l.LeadCode.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status.ToLower() == status.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(assignedTo))
            {
                query = query.Where(l => l.AssignedTo == assignedTo);
            }

            return await query.OrderByDescending(l => l.CreatedDate).ToListAsync();
        }

        public async Task<Lead?> GetByIdAsync(int id)
        {
            return await _context.Leads
                .Include(l => l.AssignedUser)
                .Include(l => l.ConvertedCustomer)
                .Include(l => l.FollowUps)
                .Include(l => l.Activities)
                .FirstOrDefaultAsync(l => l.LeadId == id);
        }

        public async Task<string> GenerateLeadCodeAsync()
        {
            var count = await _context.Leads.CountAsync() + 1;
            return $"LEAD-{count:D4}";
        }

        public async Task<(bool Success, string? Error, Lead? Lead)> CreateAsync(
            Lead lead,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            if (!ValidStatuses.Contains(lead.Status))
            {
                return (false, $"Invalid lead status: {lead.Status}", null);
            }

            if (lead.ExpectedValue < 0)
            {
                return (false, "Expected value cannot be negative.", null);
            }

            if (string.IsNullOrWhiteSpace(lead.LeadCode))
            {
                lead.LeadCode = await GenerateLeadCodeAsync();
            }

            lead.CreatedDate = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(lead.AssignedTo))
            {
                lead.AssignedTo = currentUserId;
            }

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Create",
                "Lead",
                lead.LeadId.ToString(),
                oldValue: null,
                newValue: new { lead.LeadId, lead.LeadCode, lead.LeadName, lead.Email, lead.Status, lead.ExpectedValue },
                details: $"Lead '{lead.LeadName}' created with status '{lead.Status}'.",
                ipAddress: ipAddress);

            return (true, null, lead);
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(
            Lead lead,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            var existing = await _context.Leads.FindAsync(lead.LeadId);
            if (existing == null)
            {
                return (false, "Lead not found.");
            }

            if (!ValidStatuses.Contains(lead.Status))
            {
                return (false, $"Invalid status '{lead.Status}'.");
            }

            // Workflow status transition check: cannot revert Converted lead
            if (existing.Status == "Converted" && lead.Status != "Converted")
            {
                return (false, "A Converted lead cannot be transitioned back to another status.");
            }

            if (lead.ExpectedValue < 0)
            {
                return (false, "Expected value cannot be negative.");
            }

            var oldSnapshot = new { existing.LeadName, existing.Email, existing.Phone, existing.Status, existing.Priority, existing.ExpectedValue };

            existing.LeadName = lead.LeadName;
            existing.Email = lead.Email;
            existing.Phone = lead.Phone;
            existing.CompanyName = lead.CompanyName;
            existing.Source = lead.Source;
            existing.Status = lead.Status;
            existing.Priority = lead.Priority;
            existing.ExpectedValue = lead.ExpectedValue;
            existing.Notes = lead.Notes;
            existing.AssignedTo = lead.AssignedTo;

            await _context.SaveChangesAsync();

            var newSnapshot = new { existing.LeadName, existing.Email, existing.Phone, existing.Status, existing.Priority, existing.ExpectedValue };

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Update",
                "Lead",
                existing.LeadId.ToString(),
                oldValue: oldSnapshot,
                newValue: newSnapshot,
                details: $"Lead '{existing.LeadName}' updated.",
                ipAddress: ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> DeleteAsync(
            int id,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null)
            {
                return (false, "Lead not found.");
            }

            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Delete",
                "Lead",
                id.ToString(),
                oldValue: new { lead.LeadId, lead.LeadCode, lead.LeadName },
                newValue: null,
                details: $"Lead '{lead.LeadName}' deleted.",
                ipAddress: ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error, int? CustomerId, int? OpportunityId)> ConvertLeadAsync(
            int leadId,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            var lead = await _context.Leads.FindAsync(leadId);
            if (lead == null)
            {
                return (false, "Lead not found.", null, null);
            }

            if (lead.Status == "Converted")
            {
                return (false, "Lead is already converted.", lead.ConvertedCustomerId, lead.ConvertedOpportunityId);
            }

            // Create Customer from Lead
            var newCustomer = new Customer
            {
                CustomerCode = await _customerService.GenerateCustomerCodeAsync(),
                CustomerName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                Status = "Active",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = lead.AssignedTo ?? currentUserId
            };

            // Handle uniqueness if email exists
            if (!await _customerService.IsEmailUniqueAsync(newCustomer.Email))
            {
                // Find existing customer
                var existingCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == newCustomer.Email.ToLower());
                if (existingCustomer != null)
                {
                    newCustomer = existingCustomer;
                }
            }
            else
            {
                _context.Customers.Add(newCustomer);
                await _context.SaveChangesAsync();
            }

            // Create Opportunity if ExpectedValue > 0
            Opportunity? newOpp = null;
            if (lead.ExpectedValue > 0)
            {
                newOpp = new Opportunity
                {
                    OpportunityName = $"{lead.LeadName} - Sales Deal",
                    CustomerId = newCustomer.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = lead.ExpectedValue,
                    Stage = "Qualification",
                    Probability = 20,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
                    Status = "Open",
                    Source = lead.Source,
                    Notes = $"Converted from Lead #{lead.LeadCode}. {lead.Notes}",
                    CreatedDate = DateTime.UtcNow,
                    AssignedTo = lead.AssignedTo ?? currentUserId
                };
                _context.Opportunities.Add(newOpp);
                await _context.SaveChangesAsync();
            }

            lead.Status = "Converted";
            lead.ConvertedCustomerId = newCustomer.CustomerId;
            lead.ConvertedOpportunityId = newOpp?.OpportunityId;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Convert Lead",
                "Lead",
                lead.LeadId.ToString(),
                oldValue: new { lead.LeadCode, Status = "Qualified" },
                newValue: new { lead.LeadCode, Status = "Converted", CustomerId = newCustomer.CustomerId, OpportunityId = newOpp?.OpportunityId },
                details: $"Lead '{lead.LeadName}' successfully converted to Customer #{newCustomer.CustomerCode}{(newOpp != null ? $" and Opportunity #{newOpp.OpportunityId}" : "")}.",
                ipAddress: ipAddress);

            return (true, null, newCustomer.CustomerId, newOpp?.OpportunityId);
        }
    }
}
