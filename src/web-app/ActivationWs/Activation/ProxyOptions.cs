using System.ComponentModel.DataAnnotations;
using System.Net;

namespace ActivationWs.Activation;

/// <summary>
/// Outbound proxy configuration used when contacting the Microsoft Activation Service.
/// </summary>
/// <remarks>
/// When <see cref="UseProxy"/> is <see langword="false"/> no proxy is configured explicitly and the
/// default .NET behaviour applies, i.e. the system / environment proxy settings of the host are used.
/// </remarks>
public sealed class ProxyOptions : IValidatableObject {
    public const string SectionName = "Proxy";

    /// <summary>Enables the explicit proxy configured below.</summary>
    public bool UseProxy { get; set; }

    /// <summary>Absolute proxy address, e.g. <c>http://proxy.contoso.com:8080</c>.</summary>
    public string? Address { get; set; }

    /// <summary>Bypasses the proxy for local addresses.</summary>
    public bool BypassOnLocal { get; set; } = true;

    /// <summary>Authenticates against the proxy with the credentials of the hosting process.</summary>
    public bool UseDefaultCredentials { get; set; }

    /// <summary>
    /// User name for explicit proxy authentication. Keep the password out of appsettings.json and
    /// supply it through user secrets or the <c>Proxy__Password</c> environment variable instead.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>Password matching <see cref="Username"/>.</summary>
    public string? Password { get; set; }

    /// <summary>Optional domain matching <see cref="Username"/>.</summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Builds the <see cref="IWebProxy"/> described by these options, or <see langword="null"/>
    /// when no explicit proxy is configured.
    /// </summary>
    public IWebProxy? CreateWebProxy() {
        if (!UseProxy || string.IsNullOrWhiteSpace(Address)) {
            return null;
        }

        var proxy = new WebProxy(Address, BypassOnLocal);

        if (UseDefaultCredentials) {
            proxy.UseDefaultCredentials = true;
        } else if (!string.IsNullOrWhiteSpace(Username)) {
            proxy.Credentials = string.IsNullOrWhiteSpace(Domain)
                ? new NetworkCredential(Username, Password)
                : new NetworkCredential(Username, Password, Domain);
        }

        return proxy;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
        if (!UseProxy) {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(Address)) {
            yield return new ValidationResult(
                "Proxy:Address must be set when Proxy:UseProxy is enabled.",
                [nameof(Address)]);
        } else if (!Uri.TryCreate(Address, UriKind.Absolute, out Uri? uri) ||
                   (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
            yield return new ValidationResult(
                "Proxy:Address must be an absolute http or https URI, e.g. http://proxy.contoso.com:8080.",
                [nameof(Address)]);
        }

        if (UseDefaultCredentials && !string.IsNullOrWhiteSpace(Username)) {
            yield return new ValidationResult(
                "Proxy:UseDefaultCredentials and Proxy:Username cannot be combined. Choose one authentication mode.",
                [nameof(UseDefaultCredentials), nameof(Username)]);
        }
    }
}
