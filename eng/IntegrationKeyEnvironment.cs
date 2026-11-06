using System.Reflection;
using System.Security.Cryptography;
using Mrbr.Service.KeyManager.Configuration;
using Mrbr.Service.KeyManager.Services;

namespace Mrbr.Cryptography.Integration;

// Test/benchmark process isolation only. Never shipped in either library.
internal sealed class IntegrationKeyEnvironment : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "mrbr-shamir-integration-" + Guid.NewGuid().ToString("N"));
    internal KeyService Service { get; }

    internal IntegrationKeyEnvironment()
    {
        ResetRegistry();
        var config = new KeyServiceConfig();
        foreach (var (id, type) in new[] { ((byte)0, KeyType.Block), ((byte)1, KeyType.Matrix) })
        {
            byte[] source = RandomNumberGenerator.GetBytes(4096);
            try
            {
                var entry = KeyServiceEntry.FromBytes(id, source, "565342976", type);
                if (type == KeyType.Matrix)
                    entry.MatrixSettings = new() { Width = 16, Height = 16, Depth = 16 };
                config.Add(entry);
            }
            finally { CryptographicOperations.ZeroMemory(source); }
        }
        var options = new KeyServiceOptions(Microsoft.Extensions.Options.Options.Create(config), new KeyValidationOptions
        {
            ApplicationId = "ShamirIntegration", HistoryDirectory = directory, AllowInitialEnrollment = true
        });
        Service = new KeyService(options);
    }

    internal static void ResetRegistry()
    {
        foreach (string field in new[] { "_records", "_application", "_directory", "_lastHistoryHash" })
            typeof(KeyServiceOptions).GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
        while (KeyConfigurationAudit.TryDequeue(out _)) { }
    }

    public void Dispose()
    {
        Service.Dispose();
        ResetRegistry();
        // This exact per-instance directory was created for the fixture above.
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
