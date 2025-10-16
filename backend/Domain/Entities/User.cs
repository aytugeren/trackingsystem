using System.ComponentModel.DataAnnotations;

namespace KuyumculukTakipProgrami.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public Role Role { get; set; }
}

