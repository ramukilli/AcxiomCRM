using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public interface IAuditService
    {
        Task LogAsync(
            string? userId,
            string? userName,
            string action,
            string entityName,
            string? recordId,
            object? oldValue = null,
            object? newValue = null,
            string? details = null,
            string? ipAddress = null,
            string result = "Success");

        Task<List<AuditLog>> GetLogsAsync(
            string? userId = null,
            string? module = null,
            string? action = null,
            DateTime? fromDate = null,
            DateTime? toDate = null);
    }
}
