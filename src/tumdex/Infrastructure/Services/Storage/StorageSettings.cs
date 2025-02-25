namespace Infrastructure.Services.Storage;

public class StorageSettings
{
    public string ActiveProvider { get; set; }
    public ProvidersSettings Providers { get; set; }

    public class ProvidersSettings
    {
        public ProviderSettings LocalStorage { get; set; }
        public ProviderSettings Cloudinary { get; set; }
        public ProviderSettings Google { get; set; }
        public ProviderSettings Yandex { get; set; }
    }

    public class ProviderSettings
    {
        public string Url { get; set; }
        public string CredentialsFilePath { get; set; }
    }
}