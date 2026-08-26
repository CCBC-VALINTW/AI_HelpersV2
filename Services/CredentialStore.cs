using System.Text.Json;
using AiHelpers.Data;
using AiHelpers.Data.Entities;
using AiHelpers.Data.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace AiHelpers.Services;

public class CredentialStore : ICredentialStore
{
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;

    public CredentialStore(AppDbContext db, IDataProtectionProvider dataProtectionProvider)
    {
        _db = db;
        // Purpose string is part of the encryption context - changing it invalidates existing
        // ciphertext, so treat it as a versioned constant, not something to casually edit.
        _protector = dataProtectionProvider.CreateProtector("AiHelpers.ProviderCredentials.v1");
    }

    public async Task<T?> GetDefaultAsync<T>(LlmProvider provider, CancellationToken cancellationToken = default) where T : class
    {
        var row = await _db.ProviderCredentials
            .Where(c => c.Provider == provider && c.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;

        var json = _protector.Unprotect(row.EncryptedPayload);
        return JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetDefaultAsync<T>(LlmProvider provider, string name, T payload, string createdBy, CancellationToken cancellationToken = default) where T : class
    {
        var encrypted = _protector.Protect(JsonSerializer.Serialize(payload));

        var existing = await _db.ProviderCredentials
            .Where(c => c.Provider == provider && c.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            existing.Name = name;
            existing.EncryptedPayload = encrypted;
            existing.CreatedAtUtc = DateTime.UtcNow;
            existing.CreatedBy = createdBy;
        }
        else
        {
            _db.ProviderCredentials.Add(new ProviderCredential
            {
                Provider = provider,
                Name = name,
                IsDefault = true,
                EncryptedPayload = encrypted,
                CreatedBy = createdBy
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
