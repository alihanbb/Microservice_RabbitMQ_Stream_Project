using System.ComponentModel.DataAnnotations;

namespace ShoppingCartService.API.Configuration;

public class RabbitMQConfiguration
{
    [Required(ErrorMessage = "RabbitMQ Host is required")]
    [MinLength(1, ErrorMessage = "RabbitMQ Host cannot be empty")]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535, ErrorMessage = "RabbitMQ Port must be between 1 and 65535")]
    public int Port { get; set; } = 5552;

    [Required(ErrorMessage = "RabbitMQ Username is required")]
    [MinLength(1, ErrorMessage = "RabbitMQ Username cannot be empty")]
    public string Username { get; set; } = "guest";

    [Required(ErrorMessage = "RabbitMQ Password is required")]
    [MinLength(1, ErrorMessage = "RabbitMQ Password cannot be empty")]
    public string Password { get; set; } = "guest";

    [Required(ErrorMessage = "RabbitMQ StreamName is required")]
    [MinLength(1, ErrorMessage = "RabbitMQ StreamName cannot be empty")]
    public string StreamName { get; set; } = "shopping-cart-events";

    [Range(1, 256, ErrorMessage = "RabbitMQ Partitions must be between 1 and 256")]
    public int Partitions { get; set; } = 3;
}
