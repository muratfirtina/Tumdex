namespace Infrastructure.Services.Security.JWT;

public class JwtConfigurationHolder
{
    private volatile JwtConfiguration _configuration;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Mevcut yapılandırmayı al
    public JwtConfiguration Configuration => _configuration;

    // Yapılandırmayı güncelle
    public async Task UpdateConfiguration(JwtConfiguration configuration)
    {
        await _semaphore.WaitAsync();
        try
        {
            _configuration = configuration;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}