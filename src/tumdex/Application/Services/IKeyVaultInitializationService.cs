namespace Application.Services;

public interface IKeyVaultInitializationService
{
    Task InitializeAsync();
    string GetEncryptionKey();
    string GetEncryptionIV();
}