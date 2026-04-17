using DepartmentService.Application.Common.Interfaces;
using DepartmentService.Application.DTOs;
using DepartmentService.Domain.Entities;
using MapsterMapper;
using Microsoft.AspNetCore.Mvc;

namespace DepartmentService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentRepository _repository;
    private readonly IMapper _mapper;
    private readonly ILogger<DepartmentsController> _logger;

    public DepartmentsController(IDepartmentRepository repository, IMapper mapper, ILogger<DepartmentsController> logger)
    {
        _repository = repository;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll(CancellationToken ct)
    {
        _logger.LogInformation("Fetching all departments");
        var departments = await _repository.GetAllAsync(ct);
        return Ok(_mapper.Map<IEnumerable<DepartmentDto>>(departments));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DepartmentDto>> GetById(int id, CancellationToken ct)
    {
        var department = await _repository.GetByIdAsync(id, ct);
        if (department == null) return NotFound();

        return Ok(_mapper.Map<DepartmentDto>(department));
    }

    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentDto dto, CancellationToken ct)
    {
        var department = _mapper.Map<Department>(dto);
        _logger.LogInformation("Creating new department: {DepartmentName}", department.Name);
        await _repository.AddAsync(department, ct);
        await _repository.SaveChangesAsync(ct);

        var result = _mapper.Map<DepartmentDto>(department);
        return CreatedAtAction(nameof(GetById), new { id = result.DepartmentId }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateDepartmentDto dto, CancellationToken ct)
    {
        var department = await _repository.GetByIdAsync(id, ct);
        if (department == null) return NotFound();

        _mapper.Map(dto, department);
        _repository.Update(department);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var department = await _repository.GetByIdAsync(id, ct);
        if (department == null) return NotFound();

        _repository.Delete(department);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }
}