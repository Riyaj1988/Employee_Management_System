using EmployeeService.Data;
using EmployeeService.DTOs;
using EmployeeService.Models;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Shared.Logging;

namespace EmployeeService.Controllers
{
    [ApiController]
    [Route("employees")]
    [Authorize]
    public class EmployeeController : ControllerBase
    {
        private readonly EmployeeDbContext _db;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogSender _logSender;
        private readonly ILogger<EmployeeController> _logger;

        public EmployeeController(
            EmployeeDbContext db,
            IPublishEndpoint publishEndpoint,
            ILogSender logSender,
            ILogger<EmployeeController> logger)
        {
            _db = db;
            _publishEndpoint = publishEndpoint;
            _logSender = logSender;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            _logger.LogInformation("Fetching all employees");

            var employees = await _db.Employees
                .Select(e => new EmployeeReadDto(
                    e.Id,
                    e.FirstName,
                    e.LastName,
                    e.Email,
                    e.DepartmentId,
                    e.Salary,
                    e.HireDate,
                    e.IsActive))
                .ToListAsync();

            return Ok(employees);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee == null)
            {
                _logger.LogWarning("Employee with ID {EmployeeId} not found", id);
                return NotFound();
            }

            return Ok(new EmployeeReadDto(
                employee.Id,
                employee.FirstName,
                employee.LastName,
                employee.Email,
                employee.DepartmentId,
                employee.Salary,
                employee.HireDate,
                employee.IsActive));
        }

        [HttpPost]
        public async Task<IActionResult> Create(EmployeeCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var employee = new Employee
                {
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Email = dto.Email,
                    DepartmentId = dto.DepartmentId,
                    Salary = dto.Salary,
                    HireDate = DateTime.UtcNow, 
                    IsActive = true            // Default to active
                };

                _db.Employees.Add(employee);
                await _db.SaveChangesAsync();

                // Publish Event to Message Broker
                await _publishEndpoint.Publish(new EmployeeEvent
                {
                    EmployeeId = employee.Id,
                    DepartmentId = employee.DepartmentId,
                    Salary = employee.Salary,
                    EventType = EmployeeEventType.Created,
                    InitiatedBy = GetCurrentUsername(),
                    CorrelationId = GetCorrelationId()
                });

                // Manual Centralized Log
                await _logSender.SendLogAsync(
                    message: $"Employee created: {employee.FirstName} {employee.LastName} (ID: {employee.Id})",
                    logLevel: "Information"
                );

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = employee.Id },
                    new EmployeeReadDto(
                        employee.Id,
                        employee.FirstName,
                        employee.LastName,
                        employee.Email,
                        employee.DepartmentId,
                        employee.Salary,
                        employee.HireDate,
                        employee.IsActive));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating employee");
                await _logSender.SendLogAsync(
                    message: "Error occurred while creating employee",
                    logLevel: "Error",
                    exception: ex.ToString()
                );
                throw;
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, EmployeeUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var employee = await _db.Employees.FindAsync(id);
            if (employee == null)
            {
                _logger.LogWarning("Attempted update on non-existing employee {EmployeeId}", id);
                return NotFound();
            }

            var oldDeptId = employee.DepartmentId;

            employee.FirstName = dto.FirstName;
            employee.LastName = dto.LastName;
            employee.Email = dto.Email;
            employee.DepartmentId = dto.DepartmentId;
            employee.Salary = dto.Salary;
            employee.IsActive = dto.IsActive; 

            await _db.SaveChangesAsync();

            await _publishEndpoint.Publish(new EmployeeEvent
            {
                EmployeeId = employee.Id,
                DepartmentId = employee.DepartmentId,
                OldDepartmentId = oldDeptId,
                Salary = employee.Salary,
                EventType = EmployeeEventType.Updated,
                InitiatedBy = GetCurrentUsername(),
                CorrelationId = GetCorrelationId()
            });

            await _logSender.SendLogAsync(
                message: $"Employee updated: {employee.FirstName} {employee.LastName} (ID: {employee.Id})",
                logLevel: "Information"
            );

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee == null)
            {
                _logger.LogWarning("Attempted delete on non-existing employee {EmployeeId}", id);
                return NotFound();
            }

            _db.Employees.Remove(employee);
            await _db.SaveChangesAsync();

            await _publishEndpoint.Publish(new EmployeeEvent
            {
                EmployeeId = employee.Id,
                DepartmentId = employee.DepartmentId,
                Salary = 0,
                EventType = EmployeeEventType.Deleted,
                InitiatedBy = GetCurrentUsername(),
                CorrelationId = GetCorrelationId()
            });

            await _logSender.SendLogAsync(
                message: $"Employee deleted with ID {id}",
                logLevel: "Warning"
            );

            return NoContent();
        }

        private string? GetCurrentUsername()
        {
            return User.Identity?.Name
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
        }

        private string GetCorrelationId()
        {
            return HttpContext?.Request?.Headers["X-Correlation-Id"].FirstOrDefault()
                   ?? Guid.NewGuid().ToString();
        }
    }
}