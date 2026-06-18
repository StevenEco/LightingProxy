namespace LightingProxy.Domain.Common;

public class TlsConfig
{
    public bool Enabled { get; set; }

    public bool Force { get; set; }

    public string? CertFile { get; set; }

    public string? KeyFile { get; set; }

    public string? TrustedCaFile { get; set; }

    public string? ServerName { get; set; }
}
