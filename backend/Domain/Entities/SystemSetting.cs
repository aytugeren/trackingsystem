using System.ComponentModel.DataAnnotations;

namespace KuyumculukTakipProgrami.Domain.Entities;

public class SystemSetting
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public string KeyName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}

