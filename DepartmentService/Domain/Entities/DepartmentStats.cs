using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Utilities;

namespace DepartmentService.Domain.Entities;

public class DepartmentStats
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int DepartmentId { get; set; }

    public int EmployeeCount { get; set; } = 0;

    public DateTime LastUpdated { get; set; } = TimeHelper.GetIstNow();

    // Navigation property
    public virtual Department? Department { get; set; }
}
