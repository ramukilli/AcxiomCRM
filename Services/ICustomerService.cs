using System.Collections.Generic;
using System.Threading.Tasks;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public interface ICustomerService
    {
        Task<List<Customer>> GetAllAsync(string? search = null, string? status = null, string? currentUserId = null, string? role = null);
        Task<Customer?> GetByIdAsync(int id);
        Task<bool> IsEmailUniqueAsync(string email, int? currentCustomerId = null);
        Task<bool> IsPhoneUniqueAsync(string phone, int? currentCustomerId = null);
        Task<string> GenerateCustomerCodeAsync();
        Task<(bool Success, string? Error, Customer? Customer)> CreateAsync(Customer customer, string currentUserId, string? currentUserName, string? ipAddress);
        Task<(bool Success, string? Error)> UpdateAsync(Customer customer, string currentUserId, string? currentUserName, string? ipAddress);
        Task<(bool Success, string? Error)> DeleteAsync(int id, string currentUserId, string? currentUserName, string? ipAddress);
    }
}
