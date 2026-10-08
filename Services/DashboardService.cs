using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models.ViewModels;

namespace AcxiomCRM.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardViewModel> GetDashboardMetricsAsync(string? currentUserId, string? role, string? dateFilter = null)
        {
            var vm = new DashboardViewModel();
            var isSales = role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId);

            // Customers query
            var custQuery = _context.Customers.AsQueryable();
            if (isSales) custQuery = custQuery.Where(c => c.CreatedBy == currentUserId);

            // Leads query
            var leadQuery = _context.Leads.AsQueryable();
            if (isSales) leadQuery = leadQuery.Where(l => l.AssignedTo == currentUserId);

            // Opportunities query
            var oppQuery = _context.Opportunities.AsQueryable();
            if (isSales) oppQuery = oppQuery.Where(o => o.AssignedTo == currentUserId);

            // FollowUps query
            var fuQuery = _context.FollowUps.AsQueryable();
            if (isSales) fuQuery = fuQuery.Where(f => f.AssignedTo == currentUserId);

            // Date filtering if specified
            DateTime? cutoff = null;
            var now = DateTime.UtcNow;
            if (dateFilter == "Today")
                cutoff = now.Date;
            else if (dateFilter == "ThisWeek")
                cutoff = now.Date.AddDays(-(int)now.DayOfWeek);
            else if (dateFilter == "ThisMonth")
                cutoff = new DateTime(now.Year, now.Month, 1);

            if (cutoff.HasValue)
            {
                custQuery = custQuery.Where(c => c.CreatedDate >= cutoff.Value);
                leadQuery = leadQuery.Where(l => l.CreatedDate >= cutoff.Value);
                oppQuery = oppQuery.Where(o => o.CreatedDate >= cutoff.Value);
            }

            // Aggregate metrics
            vm.TotalCustomers = await custQuery.CountAsync();
            vm.TotalLeads = await leadQuery.CountAsync();
            vm.OpenLeads = await leadQuery.Where(l => l.Status != "Converted" && l.Status != "Lost").CountAsync();

            vm.TotalOpportunities = await oppQuery.CountAsync();
            vm.OpenOpportunities = await oppQuery.Where(o => o.Status == "Open").CountAsync();
            vm.WonOpportunities = await oppQuery.Where(o => o.Stage == "Won" || o.Status == "Won").CountAsync();
            vm.LostOpportunities = await oppQuery.Where(o => o.Stage == "Lost" || o.Status == "Lost").CountAsync();

            var allOpps = await oppQuery.ToListAsync();
            var openOpps = allOpps.Where(o => o.Status == "Open").ToList();
            vm.TotalPipelineValue = openOpps.Sum(o => o.Amount);
            vm.WeightedPipelineValue = Math.Round(openOpps.Sum(o => o.Amount * o.Probability / 100m), 2);

            vm.PendingFollowUpsCount = await fuQuery.Where(f => f.Status == "Planned").CountAsync();
            vm.OverdueFollowUpsCount = await fuQuery.Where(f => f.Status == "Planned" && f.FollowUpDate < now).CountAsync();

            // Chart 1: Lead Status Chart
            var leadStatuses = new[] { "New", "Contacted", "Qualified", "Converted", "Lost" };
            var leadStatusGroup = await leadQuery
                .GroupBy(l => l.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var status in leadStatuses)
            {
                vm.LeadStatusLabels.Add(status);
                var match = leadStatusGroup.FirstOrDefault(g => string.Equals(g.Status, status, StringComparison.OrdinalIgnoreCase));
                vm.LeadStatusCounts.Add(match?.Count ?? 0);
            }

            // Chart 2: Opportunity Pipeline Stage Chart
            var oppStages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
            var oppStageGroup = allOpps
                .GroupBy(o => o.Stage)
                .Select(g => new { Stage = g.Key, Total = g.Sum(x => x.Amount) })
                .ToList();

            foreach (var stage in oppStages)
            {
                vm.OpportunityStageLabels.Add(stage);
                var match = oppStageGroup.FirstOrDefault(g => string.Equals(g.Stage, stage, StringComparison.OrdinalIgnoreCase));
                vm.OpportunityStageAmounts.Add(match?.Total ?? 0m);
            }

            // Chart 3: Monthly Sales / Performance Trend (Past 6 months)
            for (int i = 5; i >= 0; i--)
            {
                var monthDate = now.AddMonths(-i);
                var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1);
                var monthLabel = monthStart.ToString("MMM yyyy");

                var monthWonTotal = allOpps
                    .Where(o => (o.Stage == "Won" || o.Status == "Won") && o.CreatedDate >= monthStart && o.CreatedDate < monthEnd)
                    .Sum(o => o.Amount);

                vm.MonthlySalesLabels.Add(monthLabel);
                vm.MonthlySalesValues.Add(monthWonTotal);
            }

            // Feed Data
            vm.UpcomingFollowUps = await fuQuery
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Where(f => f.Status == "Planned" && f.FollowUpDate >= now.Date)
                .OrderBy(f => f.FollowUpDate)
                .Take(5)
                .ToListAsync();

            vm.OverdueFollowUps = await fuQuery
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Where(f => f.Status == "Planned" && f.FollowUpDate < now)
                .OrderBy(f => f.FollowUpDate)
                .Take(5)
                .ToListAsync();

            vm.RecentActivities = await _context.AuditLogs
                .OrderByDescending(a => a.CreatedDate)
                .Take(6)
                .ToListAsync();

            return vm;
        }
    }
}
