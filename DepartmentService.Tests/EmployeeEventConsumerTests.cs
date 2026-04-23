using DepartmentService.Infrastructure.Messaging;
using DepartmentService.Infrastructure.Persistence;
using DepartmentService.Domain.Entities;
using Shared.DTOs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Moq;
using FluentAssertions;

namespace DepartmentService.Tests;

public class EmployeeEventConsumerTests : IDisposable
{
    private readonly DepartmentDbContext _context;
    private readonly Mock<ILogger<EmployeeEventConsumer>> _loggerMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly EmployeeEventConsumer _consumer;

    public EmployeeEventConsumerTests()
    {
        var options = new DbContextOptionsBuilder<DepartmentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DepartmentDbContext(options);
        _loggerMock = new Mock<ILogger<EmployeeEventConsumer>>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _consumer = new EmployeeEventConsumer(_context, _loggerMock.Object, _httpContextAccessorMock.Object);
    }

    [Fact]
    public async Task Consume_ShouldIncrementCount_WhenEmployeeCreated()
    {
        var deptId = 1;
        _context.Departments.Add(new Department { DepartmentId = deptId, Name = "Test Dept" });
        var stats = new DepartmentStats { DepartmentId = deptId, EmployeeCount = 5 };
        _context.DepartmentStats.Add(stats);
        await _context.SaveChangesAsync();

        var message = new EmployeeEvent { DepartmentId = deptId, EmployeeId = 101, EventType = EmployeeEventType.Created };
        var contextMock = new Mock<ConsumeContext<EmployeeEvent>>();
        contextMock.Setup(c => c.Message).Returns(message);

        await _consumer.Consume(contextMock.Object);

        var updatedStats = await _context.DepartmentStats.FindAsync(deptId);
        updatedStats!.EmployeeCount.Should().Be(6);
    }

    [Fact]
    public async Task Consume_ShouldDecrementCount_WhenEmployeeDeleted()
    {
        var deptId = 2;
        _context.Departments.Add(new Department { DepartmentId = deptId, Name = "Test Dept" });
        var stats = new DepartmentStats { DepartmentId = deptId, EmployeeCount = 10 };
        _context.DepartmentStats.Add(stats);
        await _context.SaveChangesAsync();

        var message = new EmployeeEvent { DepartmentId = deptId, EmployeeId = 102, EventType = EmployeeEventType.Deleted };
        var contextMock = new Mock<ConsumeContext<EmployeeEvent>>();
        contextMock.Setup(c => c.Message).Returns(message);

        await _consumer.Consume(contextMock.Object);

        var updatedStats = await _context.DepartmentStats.FindAsync(deptId);
        updatedStats!.EmployeeCount.Should().Be(9);
    }

    [Fact]
    public async Task Consume_ShouldCreateStats_WhenRecordMissing()
    {
        var deptId = 3;
        _context.Departments.Add(new Department { DepartmentId = deptId, Name = "Test Dept" });
        await _context.SaveChangesAsync();

        var message = new EmployeeEvent { DepartmentId = deptId, EmployeeId = 103, EventType = EmployeeEventType.Created };
        var contextMock = new Mock<ConsumeContext<EmployeeEvent>>();
        contextMock.Setup(c => c.Message).Returns(message);

        await _consumer.Consume(contextMock.Object);

        var stats = await _context.DepartmentStats.FindAsync(deptId);
        stats.Should().NotBeNull();
        stats!.EmployeeCount.Should().Be(1);
    }

    [Fact]
    public async Task Consume_ShouldNotGoBelowZero_WhenDecrementing()
    {
        var deptId = 4;
        _context.Departments.Add(new Department { DepartmentId = deptId, Name = "Test Dept" });
        var stats = new DepartmentStats { DepartmentId = deptId, EmployeeCount = 0 };
        _context.DepartmentStats.Add(stats);
        await _context.SaveChangesAsync();

        var message = new EmployeeEvent { DepartmentId = deptId, EmployeeId = 104, EventType = EmployeeEventType.Deleted };
        var contextMock = new Mock<ConsumeContext<EmployeeEvent>>();
        contextMock.Setup(c => c.Message).Returns(message);

        await _consumer.Consume(contextMock.Object);

        var updatedStats = await _context.DepartmentStats.FindAsync(deptId);
        updatedStats!.EmployeeCount.Should().Be(0);
    }

    [Fact]
    public async Task Consume_ShouldUpdateBothCounts_WhenEmployeeTransfered()
    {
        var oldDeptId = 10;
        var newDeptId = 20;

        _context.Departments.AddRange(
            new Department { DepartmentId = oldDeptId, Name = "Old Dept" },
            new Department { DepartmentId = newDeptId, Name = "New Dept" }
        );

        _context.DepartmentStats.AddRange(
            new DepartmentStats { DepartmentId = oldDeptId, EmployeeCount = 5 },
            new DepartmentStats { DepartmentId = newDeptId, EmployeeCount = 3 }
        );

        await _context.SaveChangesAsync();

        var message = new EmployeeEvent 
        { 
            DepartmentId = newDeptId, 
            OldDepartmentId = oldDeptId, 
            EmployeeId = 500, 
            EventType = EmployeeEventType.Updated 
        };

        var contextMock = new Mock<ConsumeContext<EmployeeEvent>>();
        contextMock.Setup(c => c.Message).Returns(message);

        await _consumer.Consume(contextMock.Object);

        var oldStats = await _context.DepartmentStats.FindAsync(oldDeptId);
        var newStats = await _context.DepartmentStats.FindAsync(newDeptId);

        oldStats!.EmployeeCount.Should().Be(4);
        newStats!.EmployeeCount.Should().Be(4);
    }


    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

