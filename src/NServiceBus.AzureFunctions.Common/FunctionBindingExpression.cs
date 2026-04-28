namespace NServiceBus;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Resolves Azure Functions binding expressions of the form <c>%SettingName%</c> against
/// <see cref="IConfiguration"/>. Supports embedded tokens like <c>%tenantId%/queues/orders</c>
/// and multiple tokens like <c>%prefix%-%env%-%name%</c>.
/// </summary>
/// <remarks>
/// The Azure Functions Host resolves <c>%...%</c> patterns against app settings before passing
/// data to the worker. Since NServiceBus.AzureFunctions needs these values at startup
/// (before the Host resolves them for trigger configuration), this class provides the same
/// resolution using <see cref="IConfiguration"/>, which contains the same app settings.
/// </remarks>
public static partial class FunctionBindingExpression
{
    [GeneratedRegex("%([^%]+)%", RegexOptions.Compiled)]
    private static partial Regex BindingExpressionPattern();

    /// <summary>
    /// Resolves all <c>%...%</c> binding expressions in <paramref name="value"/> using
    /// <paramref name="configuration"/>. Returns the value as-is if it contains no
    /// <c>%...%</c> patterns.
    /// </summary>
    /// <param name="value">The string that may contain <c>%SettingName%</c> tokens.</param>
    /// <param name="configuration">The configuration to resolve setting names against.</param>
    /// <returns>The value with all binding expressions resolved.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a setting name referenced by a <c>%...%</c> token is not found in configuration.
    /// </exception>
    public static string Resolve(string value, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return BindingExpressionPattern().Replace(value, match =>
        {
            var settingName = match.Groups[1].Value;
            var resolved = configuration[settingName];

            return resolved ?? throw new InvalidOperationException(
                $"Function binding expression '%{settingName}%' could not be resolved. " +
                $"No configuration setting found with key '{settingName}'.");
        });
    }
}