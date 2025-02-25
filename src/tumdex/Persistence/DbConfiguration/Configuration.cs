using Microsoft.Extensions.Configuration;

namespace Persistence.DbConfiguration;

static class Configuration
{
    /*static public string ConnectionString
    {
        get
        {
            var configurationManager = new ConfigurationManager();
            configurationManager.SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../WebAPI"));
            configurationManager.AddJsonFile("appsettings.json");
            configurationManager.AddEnvironmentVariables(); // Environment variables'ı ekle

            var connectionString = configurationManager.GetConnectionString("TumdexDb");
            
            // Environment variable'ları işle
            connectionString = connectionString
                .Replace("${POSTGRES_USER}", Environment.GetEnvironmentVariable("POSTGRES_USER"))
                .Replace("${POSTGRES_PASSWORD}", Environment.GetEnvironmentVariable("POSTGRES_PASSWORD"))
                .Replace("${POSTGRES_HOST}", Environment.GetEnvironmentVariable("POSTGRES_HOST"))
                .Replace("${POSTGRES_PORT}", Environment.GetEnvironmentVariable("POSTGRES_PORT"))
                .Replace("${POSTGRES_DB}", Environment.GetEnvironmentVariable("POSTGRES_DB"));

            return connectionString;
        }
    }*/
}