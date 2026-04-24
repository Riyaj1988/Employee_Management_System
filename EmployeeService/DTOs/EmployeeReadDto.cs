namespace EmployeeService.DTOs
{
    public record EmployeeReadDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    int DepartmentId,
    decimal Salary,
    DateTime HireDate,
    bool IsActive
);

}
