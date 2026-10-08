using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public CustomerService(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<List<Customer>> GetAllAsync(string? search = null, string? status = null, string? currentUserId = null, string? role = null)
        {
            var query = _context.Customers
                .Include(c => c.Owner)
                .AsQueryable();

            // Role-based scoping: SalesExecutive only views own assigned customers
            if (role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId))
            {
                query = query.Where(c => c.CreatedBy == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c =>
                    c.CustomerName.ToLower().Contains(s) ||
                    c.Email.ToLower().Contains(s) ||
                    c.Phone.Contains(s) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(s)) ||
                    c.CustomerCode.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(c => c.Status.ToLower() == status.ToLower());
            }

            return await query.OrderByDescending(c => c.CreatedDate).ToListAsync();
        }

        public async Task<Customer?> GetByIdAsync(int id)
        {
            return await _context.Customers
                .Include(c => c.Owner)
                .Include(c => c.Opportunities)
                .Include(c => c.FollowUps)
                .Include(c => c.Activities)
                .FirstOrDefaultAsync(c => c.CustomerId == id);
        }

        public async Task<bool> IsEmailUniqueAsync(string email, int? currentCustomerId = null)
        {
            var normalized = email.Trim().ToLower();
            return !await _context.Customers.AnyAsync(c =>
                c.Email.ToLower() == normalized &&
                (!currentCustomerId.HasValue || c.CustomerId != currentCustomerId.Value));
        }

        public async Task<bool> IsPhoneUniqueAsync(string phone, int? currentCustomerId = null)
        {
            var cleanPhone = phone.Trim();
            return !await _context.Customers.AnyAsync(c =>
                c.Phone == cleanPhone &&
                (!currentCustomerId.HasValue || c.CustomerId != currentCustomerId.Value));
        }

        public async Task<string> GenerateCustomerCodeAsync()
        {
            var count = await _context.Customers.CountAsync() + 1;
            return $"CUST-{count:D4}";
        }

        public async Task<(bool Success, string? Error, Customer? Customer)> CreateAsync(
            Customer customer,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            // Business Validation 1: Email uniqueness
            if (!await IsEmailUniqueAsync(customer.Email))
            {
                return (false, "A customer with this email address already exists.", null);
            }

            // Business Validation 2: Phone uniqueness
            if (!await IsPhoneUniqueAsync(customer.Phone))
            {
                return (false, "A customer with this phone number already exists.", null);
            }

            if (string.IsNullOrWhiteSpace(customer.CustomerCode))
            {
                customer.CustomerCode = await GenerateCustomerCodeAsync();
            }

            customer.CreatedDate = DateTime.UtcNow;
            customer.CreatedBy = currentUserId;

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            // Audit record
            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Create",
                "Customer",
                customer.CustomerId.ToString(),
                oldValue: null,
                newValue: new { customer.CustomerId, customer.CustomerCode, customer.CustomerName, customer.Email, customer.Phone, customer.Status },
                details: $"Customer '{customer.CustomerName}' ({customer.CustomerCode}) created.",
                ipAddress: ipAddress);

            return (true, null, customer);
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(
            Customer customer,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            var existing = await _context.Customers.FindAsync(customer.CustomerId);
            if (existing == null)
            {
                return (false, "Customer not found.");
            }

            // Business Validation: Email uniqueness
            if (!await IsEmailUniqueAsync(customer.Email, customer.CustomerId))
            {
                return (false, "Another customer already uses this email address.");
            }

            // Business Validation: Phone uniqueness
            if (!await IsPhoneUniqueAsync(customer.Phone, customer.CustomerId))
            {
                return (false, "Another customer already uses this phone number.");
            }

            var oldSnapshot = new
            {
                existing.CustomerName,
                existing.Email,
                existing.Phone,
                existing.CompanyName,
                existing.Address,
                existing.City,
                existing.State,
                existing.Status
            };

            existing.CustomerName = customer.CustomerName;
            existing.Email = customer.Email;
            existing.Phone = customer.Phone;
            existing.CompanyName = customer.CompanyName;
            existing.Address = customer.Address;
            existing.City = customer.City;
            existing.State = customer.State;
            existing.Status = customer.Status;
            existing.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var newSnapshot = new
            {
                existing.CustomerName,
                existing.Email,
                existing.Phone,
                existing.CompanyName,
                existing.Address,
                existing.City,
                existing.State,
                existing.Status
            };

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Update",
                "Customer",
                existing.CustomerId.ToString(),
                oldValue: oldSnapshot,
                newValue: newSnapshot,
                details: $"Customer '{existing.CustomerName}' updated.",
                ipAddress: ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> DeleteAsync(
            int id,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return (false, "Customer not found.");
            }

            var oldSnapshot = new { customer.CustomerId, customer.CustomerCode, customer.CustomerName, customer.Email };

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Delete",
                "Customer",
                id.ToString(),
                oldValue: oldSnapshot,
                newValue: null,
                details: $"Customer '{customer.CustomerName}' deleted.",
                ipAddress: ipAddress);

            return (true, null);
        }
    }
}
