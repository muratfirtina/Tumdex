namespace Application.Abstraction.Services.Configurations;

public interface IKeyVaultService
{
    /// <summary>
    /// Gets a secret from Azure Key Vault with the specified name
    /// </summary>
    /// <param name="secretName">Name of the secret</param>
    /// <returns>Secret value</returns>
    Task<string> GetSecretAsync(string secretName);

    /// <summary>
    /// Gets multiple secrets from Azure Key Vault
    /// </summary>
    /// <param name="secretNames">Array of secret names to retrieve</param>
    /// <returns>Dictionary containing secret names and their values</returns>
    Task<Dictionary<string, string>> GetSecretsAsync(string[] secretNames);

    /// <summary>
    /// Adds or updates a secret in Azure Key Vault
    /// </summary>
    /// <param name="secretName">Name of the secret</param>
    /// <param name="value">Value of the secret</param>
    /// <param name="recoverIfDeleted">Try to recover if the secret was deleted</param>
    Task SetSecretAsync(string secretName, string value, bool recoverIfDeleted = false);

    /// <summary>
    /// Adds or updates multiple secrets in Azure Key Vault
    /// </summary>
    /// <param name="secrets">Dictionary containing secret names and values</param>
    /// <param name="recoverIfDeleted">Try to recover if any secrets were deleted</param>
    Task SetSecretsAsync(Dictionary<string, string> secrets, bool recoverIfDeleted = false);

    /// <summary>
    /// Gets all secrets from Azure Key Vault
    /// </summary>
    /// <returns>Dictionary containing all secret names and their values</returns>
    Task<Dictionary<string, string>> GetAllSecretsAsync();

    /// <summary>
    /// Deletes a secret from Azure Key Vault
    /// </summary>
    /// <param name="secretName">Name of the secret to delete</param>
    Task DeleteSecretAsync(string secretName);

    /// <summary>
    /// Deletes multiple secrets from Azure Key Vault
    /// </summary>
    /// <param name="secretNames">Array of secret names to delete</param>
    /// <returns>Dictionary with results of each deletion operation</returns>
    Task<Dictionary<string, bool>> DeleteSecretsAsync(string[] secretNames);
}