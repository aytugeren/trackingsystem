namespace KuyumculukTakipProgrami.Domain.Entities;

public class CategoryProduct
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public Guid ProductId { get; set; }

    public Category? Category { get; set; }
    public Product? Product { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? CreatedUserId { get; set; }
    public Guid? UpdatedUserId { get; set; }
}
