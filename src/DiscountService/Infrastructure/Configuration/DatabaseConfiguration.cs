using System.ComponentModel.DataAnnotations;

namespace DiscountService.Infrastructure.Configuration;

/// <summary>
/// Database configuration validated at startup.
/// </summary>
public class DatabaseConfiguration
{
    [Required(ErrorMessage = "Database connection string is required")]
    [MinLength(5, ErrorMessage = "Database connection string must be provided")]
    public string ConnectionString { get; set; } = "Server=(local);Database=DiscountDB;Trusted_Connection=true;";
}
