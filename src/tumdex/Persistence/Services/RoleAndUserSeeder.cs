using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Persistence.Services;

public static class RoleAndUserSeeder
{
    public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        string[] roles = { "Admin", "User" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new AppRole
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = role,
                    NormalizedName = role.ToUpper()
                });
            }
        }
    }

    private static async Task SeedAdminUserAsync(
        IServiceProvider serviceProvider, 
        IConfiguration configuration)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();

        // Admin bilgilerini Key Vault'tan al
        var adminUsername = configuration["AdminUsername"] 
                            ?? throw new InvalidOperationException("Admin kullanıcı adı bulunamadı");
        var adminEmail = configuration["AdminEmail"] 
                         ?? throw new InvalidOperationException("Admin email adresi bulunamadı");
        var adminPassword = configuration["AdminPassword"] 
                            ?? throw new InvalidOperationException("Admin şifresi bulunamadı");

        var adminUser = await userManager.FindByNameAsync(adminUsername);
        
        if (adminUser == null)
        {
            adminUser = new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = adminUsername,
                Email = adminEmail,
                EmailConfirmed = true,
                NameSurname = configuration["AdminNameSurname"] 
                              ?? throw new InvalidOperationException("Admin adı soyadı bulunamadı")
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
            else
            {
                throw new Exception(
                    $"Admin kullanıcısı oluşturulamadı: {string.Join(", ", result.Errors)}");
            }
        }
    }


    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DatabaseMigrationService>>();

        try
        {
            await SeedRolesAsync(scope.ServiceProvider);
            await SeedAdminUserAsync(scope.ServiceProvider, configuration);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seed işlemi sırasında bir hata oluştu");
            throw;
        }
    }
}