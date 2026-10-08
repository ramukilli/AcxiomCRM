using System.Collections.Generic;
using AcxiomCRM.Models;

namespace AcxiomCRM.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalCustomers { get; set; }
        public int TotalLeads { get; set; }
        public int OpenLeads { get; set; }
        public int TotalOpportunities { get; set; }
        public int OpenOpportunities { get; set; }
        public int WonOpportunities { get; set; }
        public int LostOpportunities { get; set; }
        public decimal TotalPipelineValue { get; set; }
        public decimal WeightedPipelineValue { get; set; }
        public int PendingFollowUpsCount { get; set; }
        public int OverdueFollowUpsCount { get; set; }

        // Chart Data
        public List<string> LeadStatusLabels { get; set; } = new();
        public List<int> LeadStatusCounts { get; set; } = new();

        public List<string> OpportunityStageLabels { get; set; } = new();
        public List<decimal> OpportunityStageAmounts { get; set; } = new();

        public List<string> MonthlySalesLabels { get; set; } = new();
        public List<decimal> MonthlySalesValues { get; set; } = new();

        // Feed
        public List<FollowUp> UpcomingFollowUps { get; set; } = new();
        public List<FollowUp> OverdueFollowUps { get; set; } = new();
        public List<AuditLog> RecentActivities { get; set; } = new();
    }
}
