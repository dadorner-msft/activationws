using System.ComponentModel.DataAnnotations;

namespace ActivationWs.Storage;

/// <summary>
/// Configuration for the local SQLite activation store.
/// </summary>
public sealed class ActivationStoreOptions {
    public const string SectionName = "ActivationStore";

    /// <summary>
    /// ADO.NET connection string for the SQLite database. A relative <c>Data Source</c> is resolved
    /// against the application content root.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = "Data Source=App_Data/ActivationWs.db";
}
