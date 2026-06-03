namespace NServiceBus;

/// <summary>
/// Provides the name of the configuration key used to resolve the transport connection.
/// Implemented by manifest types produced by the source generator.
/// </summary>
/// <remarks>The API surface might be changed between versions according to the needs of the source generator.</remarks>
public interface IConnectionSettingManifest
{
    /// <summary>
    /// The name of the application setting or configuration section that contains the transport connection details,
    /// or <see langword="null" /> to use the transport's default.
    /// </summary>
    string? ConnectionSettingName { get; }
}