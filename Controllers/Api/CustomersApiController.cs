using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using AcxiomCRM.Models.DTOs;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/customers")]
    [Produces("application/json")]
    public class CustomersApiController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomersApiController(
            ICustomerService customerService,
            UserManager<ApplicationUser> userManager)
        {
            _customerService = customerService;
            _userManager = userManager;
        }

        private static CustomerResponseDto MapToDto(Customer c) => new()
        {
            CustomerId = c.CustomerId,
            CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName,
            Email = c.Email,
            Phone = c.Phone,
            CompanyName = c.CompanyName,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Status = c.Status,
            CreatedDate = c.CreatedDate,
            OwnerName = c.Owner?.FullName ?? c.Owner?.UserName
        };

        // GET: /api/customers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerResponseDto>>> GetCustomers([FromQuery] string? search, [FromQuery] string? status)
        {
            var customers = await _customerService.GetAllAsync(search, status);
            return Ok(customers.Select(MapToDto));
        }

        // GET: /api/customers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerResponseDto>> GetCustomer(int id)
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null)
            {
                return NotFound(new { message = $"Customer with ID {id} not found." });
            }
            return Ok(MapToDto(customer));
        }

        // POST: /api/customers
        [HttpPost]
        public async Task<ActionResult<CustomerResponseDto>> CreateCustomer([FromBody] CreateCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!await _customerService.IsEmailUniqueAsync(dto.Email))
            {
                return Conflict(new { message = "A customer with this email address already exists." });
            }

            if (!await _customerService.IsPhoneUniqueAsync(dto.Phone))
            {
                return Conflict(new { message = "A customer with this phone number already exists." });
            }

            var customer = new Customer
            {
                CustomerName = dto.CustomerName,
                Email = dto.Email,
                Phone = dto.Phone,
                CompanyName = dto.CompanyName,
                Address = dto.Address,
                City = dto.City,
                State = dto.State,
                Status = dto.Status
            };

            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id ?? "api-user";
            var userName = user?.UserName ?? "REST-API";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error, created) = await _customerService.CreateAsync(customer, userId, userName, ip);
            if (!success || created == null)
            {
                return BadRequest(new { message = error ?? "Failed to create customer." });
            }

            var responseDto = MapToDto(created);
            return CreatedAtAction(nameof(GetCustomer), new { id = created.CustomerId }, responseDto);
        }

        // PUT: /api/customers/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existing = await _customerService.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new { message = $"Customer with ID {id} not found." });
            }

            if (!await _customerService.IsEmailUniqueAsync(dto.Email, id))
            {
                return Conflict(new { message = "Another customer already uses this email address." });
            }

            if (!await _customerService.IsPhoneUniqueAsync(dto.Phone, id))
            {
                return Conflict(new { message = "Another customer already uses this phone number." });
            }

            existing.CustomerName = dto.CustomerName;
            existing.Email = dto.Email;
            existing.Phone = dto.Phone;
            existing.CompanyName = dto.CompanyName;
            existing.Address = dto.Address;
            existing.City = dto.City;
            existing.State = dto.State;
            existing.Status = dto.Status;

            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id ?? "api-user";
            var userName = user?.UserName ?? "REST-API";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error) = await _customerService.UpdateAsync(existing, userId, userName, ip);
            if (!success)
            {
                return BadRequest(new { message = error ?? "Failed to update customer." });
            }

            return Ok(MapToDto(existing));
        }

        // DELETE: /api/customers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id ?? "api-user";
            var userName = user?.UserName ?? "REST-API";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var (success, error) = await _customerService.DeleteAsync(id, userId, userName, ip);
            if (!success)
            {
                return NotFound(new { message = error ?? $"Customer with ID {id} not found." });
            }

            return Ok(new { message = $"Customer with ID {id} successfully deleted." });
        }
    }
}
