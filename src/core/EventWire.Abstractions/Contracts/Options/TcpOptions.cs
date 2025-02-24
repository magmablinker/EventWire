using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace EventWire.Abstractions.Contracts.Options;

public sealed class TcpOptions
{
    public TcpOptions()
    {
        Certificate = new(GetCert);
    }

    public IPAddress IpAddress { get; set; } = null!;
    public int Port { get; set; }
    public HashSet<string> ApiKeys { get; set; } = null!;
    public string ServerName { get; set; } = null!;
    public string CertPath { get; set; } = null!;
    public string CertPassword { get; set; } = null!;
    public Lazy<X509Certificate2> Certificate;

    private X509Certificate2 GetCert() => new(CertPath, CertPassword, X509KeyStorageFlags.MachineKeySet);
}
