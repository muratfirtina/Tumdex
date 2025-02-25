namespace Core.Persistence.Repositories;

public abstract class Entity<TId> : IEntity<TId>, IEntityTimestamps
{
    public TId Id { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public DateTime? DeletedDate { get; set; }

    // Parametresiz yapıcı metod
    protected Entity()
    {
        Id = default!;
    }

    // Tek parametreli yapıcı metod (Brand, Category için)
    protected Entity(string? name)
    {
        Id = (TId)(object)IdGenerator.GenerateId(name);
        CreatedDate = DateTime.UtcNow;
    }
    
    // İki parametreli yapıcı metod (Product için)
    protected Entity(string? name, string? sku)
    {
        Id = (TId)(object)IdGenerator.GenerateIdwithSku(name, sku);
        CreatedDate = DateTime.UtcNow;
    }
}