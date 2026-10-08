using System.Threading.Tasks;
using AcxiomCRM.Models.ViewModels;

namespace AcxiomCRM.Services
{
    public interface IDashboardService
    {
        Task<DashboardViewModel> GetDashboardMetricsAsync(string? currentUserId, string? role, string? dateFilter = null);
    }
}
