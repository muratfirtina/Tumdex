using System.Diagnostics;
using Core.Persistence.Repositories.Operation;

namespace Core.Persistence.Repositories;

public static class IdGenerator
{
    public static string GenerateId(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return Guid.NewGuid().ToString("N");

        // Entity tipini belirle
        string prefix = DeterminePrefix();
        
        // Marka için özel durum
        if (prefix == "b")
        {
            return NameOperation.CharacterRegulatory(name.ToLower());
        }
        
        // Diğer entityler için (Product ve Category)
        string cleanedName = NameOperation.CharacterRegulatory(name.ToLower());
        string randomPart = Guid.NewGuid().ToString("N").Substring(0, 8);
        return $"{cleanedName}-{prefix}-{randomPart}";
    }

    public static string GenerateIdwithSku(string? name, string? sku)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(sku))
            return Guid.NewGuid().ToString("N");
        
        string cleanedName = NameOperation.CharacterRegulatory(name.ToLower());
        string cleanedSku = NameOperation.CharacterRegulatory(sku.ToLower());
        string randomPart = Guid.NewGuid().ToString("N").Substring(0, 8);
        return $"{cleanedName}-{cleanedSku}-p-{randomPart}";
    }

    private static string DeterminePrefix()
    {
        var stackTrace = new StackTrace();
        var frames = stackTrace.GetFrames();
        
        foreach (var frame in frames)
        {
            var type = frame.GetMethod()?.DeclaringType;
            
            if (type != null)
            {
                var typeName = type.Name.ToLower();
                if (typeName.Contains("brand")) return "b";
                if (typeName.Contains("category")) return "c";
                if (typeName.Contains("product")) return "p";
            }
        }
        
        return "item";
    }
}