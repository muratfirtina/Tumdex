namespace Core.Persistence.Repositories;

public abstract class ProductEntity<TId> : IEntity<TId>, IEntityTimestamps
{
    public TId Id { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public DateTime? DeletedDate { get; set; }

    protected ProductEntity()
    {
        Id = default!;
    }

    protected ProductEntity(string? name, string? sku)
    {
        Id = (TId)(object)IdGenerator.GenerateIdwithSku(name, sku);
        CreatedDate = DateTime.UtcNow;
    }
}  