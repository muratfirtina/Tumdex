namespace Domain;

public class ProductFeatureValue
{
    public string ProductId { get; set; }
    public Product Product { get; set; }
    public string FeatureValueId { get; set; }
    public FeatureValue FeatureValue { get; set; }
    
    public ProductFeatureValue(string productId, string featureValueId)
    {
        ProductId = productId;
        FeatureValueId = featureValueId;
    }

    public ProductFeatureValue()
    {
        
    }
}