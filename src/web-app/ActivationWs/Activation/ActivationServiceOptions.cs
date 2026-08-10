using System.ComponentModel.DataAnnotations;

namespace ActivationWs.Activation;

/// <summary>
/// Configuration for the Microsoft Activation Service client.
/// </summary>
public sealed class ActivationServiceOptions {
    public const string SectionName = "ActivationService";

    /// <summary>
    /// Endpoint of the Microsoft Activation Service.
    /// </summary>
    [Required]
    public Uri Endpoint { get; set; } = new("https://activation.sls.microsoft.com/BatchActivation/BatchActivation.asmx");

    /// <summary>
    /// SOAPAction header value sent with every request.
    /// </summary>
    [Required]
    public string SoapAction { get; set; } = "http://www.microsoft.com/BatchActivationService/BatchActivate";

    /// <summary>
    /// Per-request timeout.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
