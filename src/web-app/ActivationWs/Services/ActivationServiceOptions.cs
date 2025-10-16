using System.ComponentModel.DataAnnotations;

namespace ActivationWs.Services
{
    public sealed class ActivationServiceOptions
    {
        [Range(1, 300, ErrorMessage = "Timeout must be between 1 and 300 seconds")]
        public int TimeoutSeconds { get; set; } = 100;
        
        public ProxyOptions? Proxy { get; set; }

        public sealed class ProxyOptions {
            public bool UseProxy { get; set; }
            
            [Url(ErrorMessage = "Proxy address must be a valid URL")]
            public string? Address { get; set; }
            public bool BypassOnLocal { get; set; } = true;
            public bool UseDefaultCredentials { get; set; }
            public string? Username { get; set; }
            public string? Password { get; set; }
            public string? Domain { get; set; }
        }
    }
}