namespace Domain;

public class CategoryFeature
{
    public string CategoryId { get; set; }
    public Category Category { get; set; }
        
    public string FeatureId { get; set; }
    public Feature Feature { get; set; }
}