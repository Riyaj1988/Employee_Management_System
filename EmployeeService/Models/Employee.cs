namespace EmployeeService.Models
{
    public class Employee
    {
        public int Id { get; set; } 
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public int DepartmentId { get; set; }
        public decimal Salary { get; set; }
        public DateTime HireDate { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}