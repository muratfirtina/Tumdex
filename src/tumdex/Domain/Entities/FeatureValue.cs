using Core.Persistence.Repositories;

namespace Domain;


public class FeatureValue : Entity<string>
{
    public string? Name { get; set; }
    public string? FeatureId { get; set; }
    public Feature? Feature { get; set; }
    public ICollection<ProductFeatureValue> ProductFeatureValues { get; set; }

    public FeatureValue(string name, string? featureId) : base(name)
    {
        Name = name;
        FeatureId = featureId;
        ProductFeatureValues = new List<ProductFeatureValue>();
    }

    public FeatureValue()
    {
        ProductFeatureValues = new List<ProductFeatureValue>();
    }
}