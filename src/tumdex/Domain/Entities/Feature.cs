using System.Collections;
using Core.Persistence.Repositories;

namespace Domain;

public class Feature : Entity<string>
{
    public string? Name { get; set; }
    public ICollection<Category>Categories { get; set; }
    public ICollection<FeatureValue> FeatureValues { get; set; }
   
    
    public Feature(string? name) : base(name)
    {
        Name = name;
        Categories = new List<Category>();
        FeatureValues = new List<FeatureValue>();
    }
}