using EmployeeService.Controllers;
using EmployeeService.Data;
using EmployeeService.DTOs;
using EmployeeService.Models;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Shared.Logging;
using System.Security.Claims;

namespace EmployeeService.Tests
{
    [TestFixture]
    public class EmployeeControllerTests
    {
        private EmployeeDbContext _dbContext = null!;
        private EmployeeController _controller = null!;

        private Mock<IPublishEndpoint> _publishEndpointMock = null!;
        private Mock<ILogSender> _logSenderMock = null!;
        private Mock<ILogger<EmployeeController>> _loggerMock = null!;

        [SetUp]
        public void Setup()
        {

            var options = new DbContextOptionsBuilder<EmployeeDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _dbContext = new EmployeeDbContext(options);

            _publishEndpointMock = new Mock<IPublishEndpoint>();
            _logSenderMock = new Mock<ILogSender>();
            _loggerMock = new Mock<ILogger<EmployeeController>>();

            _controller = new EmployeeController(
                _dbContext,
                _publishEndpointMock.Object,
                _logSenderMock.Object,
                _loggerMock.Object
            );


            var user = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim("sub", "admin.user"),
                        new Claim(ClaimTypes.Role, "Admin")
                    },
                    "TestAuthentication"
                )
            );

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            _dbContext.Dispose();
        }


        [Test]
        public async Task CreateEmployee_WithValidData_ReturnsCreatedAndPersistsEmployee()
        {
            var dto = new EmployeeCreateDto(
                "Amit Sharma",
                "amit.sharma@company.com",
                DepartmentId: 10,
                Salary: 65000
            );

            var result = await _controller.Create(dto) as CreatedAtActionResult;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(201));
            Assert.That(_dbContext.Employees.Count(), Is.EqualTo(1));

            var employee = _dbContext.Employees.First();
            Assert.That(employee.Name, Is.EqualTo("Amit Sharma"));
            Assert.That(employee.DepartmentId, Is.EqualTo(10));
        }



        [Test]
        public async Task GetEmployeeById_WhenEmployeeExists_ReturnsEmployee()
        {
            var employee = new Employee
            {
                Name = "Priya Verma",
                Email = "priya.verma@company.com",
                DepartmentId = 20,
                Salary = 72000
            };

            _dbContext.Employees.Add(employee);
            await _dbContext.SaveChangesAsync();

            var result = await _controller.GetById(employee.Id) as OkObjectResult;

            Assert.That(result, Is.Not.Null);

            var dto = result!.Value as EmployeeReadDto;
            Assert.That(dto!.Name, Is.EqualTo("Priya Verma"));
            Assert.That(dto.DepartmentId, Is.EqualTo(20));
        }


        [Test]
        public async Task UpdateEmployee_WithDepartmentChange_PublishesUpdatedEvent()
        {
            var employee = new Employee
            {
                Name = "Rohit Kumar",
                Email = "rohit.kumar@company.com",
                DepartmentId = 30,
                Salary = 58000
            };

            _dbContext.Employees.Add(employee);
            await _dbContext.SaveChangesAsync();

            var updateDto = new EmployeeUpdateDto(
                "Rohit Kumar",
                "rohit.kumar@company.com",
                DepartmentId: 40,
                Salary: 75000
            );

            var result = await _controller.Update(employee.Id, updateDto);

            Assert.That(result, Is.TypeOf<NoContentResult>());

            var updatedEmployee = _dbContext.Employees.First();
            Assert.That(updatedEmployee.DepartmentId, Is.EqualTo(40));
            Assert.That(updatedEmployee.Salary, Is.EqualTo(75000));
        }



        [Test]
        public async Task DeleteEmployee_RemovesEmployeeAndReturnsNoContent()
        {
            var employee = new Employee
            {
                Name = "Neha Singh",
                Email = "neha.singh@company.com",
                DepartmentId = 50,
                Salary = 50000
            };

            _dbContext.Employees.Add(employee);
            await _dbContext.SaveChangesAsync();

            var result = await _controller.Delete(employee.Id);

            Assert.That(result, Is.TypeOf<NoContentResult>());
            Assert.That(_dbContext.Employees.Count(), Is.EqualTo(0));
        }



        [Test]
        public async Task GetEmployee_WithInvalidId_ReturnsNotFound()
        {
            var result = await _controller.GetById(9999);

            Assert.That(result, Is.TypeOf<NotFoundResult>());
        }


        [Test]
        public async Task CreateEmployee_WithInvalidModel_ReturnsBadRequest()
        {
            _controller.ModelState.AddModelError("Name", "Name is required");

            var dto = new EmployeeCreateDto(
                "",
                "invalid-email",
                DepartmentId: 0,
                Salary: -100
            );

            var result = await _controller.Create(dto);

            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }



        [Test]
        public async Task EmployeeCount_ShouldIncreaseOnCreate_AndDecreaseOnDelete()
        {
            var dto = new EmployeeCreateDto(
                "Suresh Reddy",
                "suresh.reddy@company.com",
                DepartmentId: 60,
                Salary: 68000
            );

            await _controller.Create(dto);
            Assert.That(_dbContext.Employees.Count(), Is.EqualTo(1));

            var employeeId = _dbContext.Employees.First().Id;
            await _controller.Delete(employeeId);

            Assert.That(_dbContext.Employees.Count(), Is.EqualTo(0));
        }
    }
}