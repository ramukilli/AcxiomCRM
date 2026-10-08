using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Customer>> GetCustomerReportAsync(string? status, DateTime? fromDate, DateTime? toDate, string? currentUserId, string? role)
        {
            var query = _context.Customers.Include(c => c.Owner).AsQueryable();

            if (role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId))
            {
                query = query.Where(c => c.CreatedBy == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(c => c.Status.ToLower() == status.ToLower());
            }

            if (fromDate.HasValue) query = query.Where(c => c.CreatedDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(c => c.CreatedDate <= toDate.Value);

            return await query.OrderByDescending(c => c.CreatedDate).ToListAsync();
        }

        public async Task<List<Lead>> GetLeadReportAsync(string? status, string? source, DateTime? fromDate, DateTime? toDate, string? currentUserId, string? role)
        {
            var query = _context.Leads.Include(l => l.AssignedUser).Include(l => l.ConvertedCustomer).AsQueryable();

            if (role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId))
            {
                query = query.Where(l => l.AssignedTo == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status.ToLower() == status.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(source))
            {
                query = query.Where(l => l.Source.ToLower() == source.ToLower());
            }

            if (fromDate.HasValue) query = query.Where(l => l.CreatedDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(l => l.CreatedDate <= toDate.Value);

            return await query.OrderByDescending(l => l.CreatedDate).ToListAsync();
        }

        public async Task<List<Opportunity>> GetOpportunityReportAsync(string? stage, DateTime? fromDate, DateTime? toDate, string? currentUserId, string? role)
        {
            var query = _context.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).AsQueryable();

            if (role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId))
            {
                query = query.Where(o => o.AssignedTo == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(stage))
            {
                query = query.Where(o => o.Stage.ToLower() == stage.ToLower());
            }

            if (fromDate.HasValue) query = query.Where(o => o.CreatedDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(o => o.CreatedDate <= toDate.Value);

            return await query.OrderByDescending(o => o.CreatedDate).ToListAsync();
        }

        public async Task<List<FollowUp>> GetFollowUpReportAsync(string? status, string? type, DateTime? fromDate, DateTime? toDate, string? currentUserId, string? role)
        {
            var query = _context.FollowUps.Include(f => f.Customer).Include(f => f.Lead).Include(f => f.AssignedUser).AsQueryable();

            if (role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId))
            {
                query = query.Where(f => f.AssignedTo == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(f => f.Status.ToLower() == status.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(f => f.FollowUpType.ToLower() == type.ToLower());
            }

            if (fromDate.HasValue) query = query.Where(f => f.FollowUpDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(f => f.FollowUpDate <= toDate.Value);

            return await query.OrderBy(f => f.FollowUpDate).ToListAsync();
        }

        public async Task<List<PipelineReportItem>> GetPipelineReportAsync(string? currentUserId, string? role)
        {
            var query = _context.Opportunities.AsQueryable();

            if (role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId))
            {
                query = query.Where(o => o.AssignedTo == currentUserId);
            }

            var opps = await query.ToListAsync();

            var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
            var list = new List<PipelineReportItem>();

            foreach (var stg in stages)
            {
                var stageOpps = opps.Where(o => string.Equals(o.Stage, stg, StringComparison.OrdinalIgnoreCase)).ToList();
                list.Add(new PipelineReportItem
                {
                    Stage = stg,
                    OpportunityCount = stageOpps.Count,
                    TotalAmount = stageOpps.Sum(o => o.Amount),
                    WeightedAmount = Math.Round(stageOpps.Sum(o => o.Amount * o.Probability / 100m), 2)
                });
            }

            return list;
        }

        public async Task<ConversionReportItem> GetConversionReportAsync(string? currentUserId, string? role)
        {
            var leadQuery = _context.Leads.AsQueryable();
            var oppQuery = _context.Opportunities.AsQueryable();

            if (role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId))
            {
                leadQuery = leadQuery.Where(l => l.AssignedTo == currentUserId);
                oppQuery = oppQuery.Where(o => o.AssignedTo == currentUserId);
            }

            var totalLeads = await leadQuery.CountAsync();
            var convertedLeads = await leadQuery.Where(l => l.Status == "Converted").CountAsync();
            var totalOpps = await oppQuery.CountAsync();
            var wonOppsList = await oppQuery.Where(o => o.Stage == "Won" || o.Status == "Won").ToListAsync();
            var wonOpps = wonOppsList.Count;
            var wonRevenue = wonOppsList.Sum(o => o.Amount);

            return new ConversionReportItem
            {
                TotalLeads = totalLeads,
                ConvertedLeads = convertedLeads,
                LeadConversionRate = totalLeads > 0 ? Math.Round((decimal)convertedLeads / totalLeads * 100m, 1) : 0,
                TotalOpportunities = totalOpps,
                WonOpportunities = wonOpps,
                OpportunityWinRate = totalOpps > 0 ? Math.Round((decimal)wonOpps / totalOpps * 100m, 1) : 0,
                TotalWonRevenue = wonRevenue
            };
        }

        public async Task<List<UserActivityReportItem>> GetUserActivityReportAsync()
        {
            var logs = await _context.AuditLogs.ToListAsync();
            var users = await _context.Users.ToListAsync();

            var report = new List<UserActivityReportItem>();

            foreach (var user in users)
            {
                var userLogs = logs.Where(l => l.UserId == user.Id || l.UserName == user.UserName).ToList();
                var userRoles = await _context.UserRoles
                    .Where(ur => ur.UserId == user.Id)
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .ToListAsync();

                report.Add(new UserActivityReportItem
                {
                    UserId = user.Id,
                    UserName = user.FullName ?? user.UserName ?? "Unknown",
                    Role = string.Join(", ", userRoles),
                    TotalActions = userLogs.Count,
                    LoginsCount = userLogs.Count(l => l.Action.Contains("Login")),
                    CreatesCount = userLogs.Count(l => l.Action == "Create"),
                    UpdatesCount = userLogs.Count(l => l.Action == "Update"),
                    DeletesCount = userLogs.Count(l => l.Action == "Delete"),
                    LastActiveDate = userLogs.OrderByDescending(l => l.CreatedDate).FirstOrDefault()?.CreatedDate
                });
            }

            return report.OrderByDescending(r => r.TotalActions).ToList();
        }
    }
}
