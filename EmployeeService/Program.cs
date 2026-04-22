using EmployeeService.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Logging;
using Shared.Middleware;
using MassTransit;
using DepartmentService.Infrastructure.Persistence;
using DepartmentService.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<EmployeeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Database")));

builder.Services.AddDbContext<DepartmentDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<EmployeeEventConsumer>();
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddSwaggerGen();
builder.AddCentralLogging("EmployeeService");
builder.Logging.AddCentralLogger(
    serviceName: "EmployeeService",
    loggingUrl: builder.Configuration["LoggingServiceUrl"]!
);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapControllers();
app.Run();