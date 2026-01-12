namespace KuyumculukTakipProgrami.Domain.Entities;

public class ProductOpeningInventory
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public DateTime Date { get; set; }
    public decimal Quantity { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedUserId { get; set; }
    public Guid? UpdatedUserId { get; set; }
}
