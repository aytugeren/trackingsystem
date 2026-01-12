using System.ComponentModel.DataAnnotations;

namespace KuyumculukTakipProgrami.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();

    public ICollection<CategoryProduct> CategoryProducts { get; set; } = new List<CategoryProduct>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? CreatedUserId { get; set; }
    public Guid? UpdatedUserId { get; set; }
}
