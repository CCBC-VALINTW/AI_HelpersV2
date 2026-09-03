using AiHelpers.Data.Enums;

namespace AiHelpers.Data.Entities;

/// <summary>
/// An admin-defined connection to an external data source, selectable by any Helper editor when
/// attaching a HelperDataQuery - same ownership shape as LlmDefinition (admin-created, globally
/// selectable, no per-user ACL of its own). The real access-control lever is what database
/// account the connection string authenticates as - see EncryptedConnectionString's own doc
/// comment.
/// </summary>
public class DataConnection
{
    public int Id { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }

    public DataConnectionType Type { get; set; } = DataConnectionType.OdbcDatabase;

    /// <summary>ASP.NET Core Data Protection ciphertext of the raw ODBC connection string - never
    /// a plain column, never logged, never round-tripped to the browser once saved (the admin
    /// page only ever shows a masked preview). This is NOT the real security boundary for what a
    /// query can do - the account embedded in the connection string is. Every Data Connection
    /// should authenticate as a dedicated, least-privilege (ideally read-only) account on its
    /// target system, provisioned specifically for this purpose - the admin form says so
    /// explicitly, this comment is the enforcement-can't-be-automated reminder.</summary>
    public required string EncryptedConnectionString { get; set; }

    /// <summary>Lets a connection be taken out of service (credential rotated, system
    /// decommissioned) without deleting it and orphaning every HelperDataQuery that references
    /// it. A Helper run against a disabled connection fails loudly with a specific message rather
    /// than silently skipping the data source.</summary>
    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    /// <summary>Set by the admin page's own "Test connection" action - not refreshed by any real
    /// Helper run, so this can go stale (a connection that worked yesterday isn't guaranteed to
    /// work now). Purely an admin-facing sanity check, never relied on to gate a real run.</summary>
    public DateTime? LastTestedAtUtc { get; set; }
    public bool? LastTestSucceeded { get; set; }
    public string? LastTestMessage { get; set; }

    public ICollection<HelperDataQuery> DataQueries { get; set; } = [];
}
