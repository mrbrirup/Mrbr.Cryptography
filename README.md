# Mrbr.Cryptography

Threshold secret sharing for .NET 11, with separate Pedersen verification and
Ed25519 commitment authentication. Feed in bytes, create shares, and reconstruct
the original data when enough shares are available. The cryptographic libraries
perform no file, network or console I/O.

[MIT licence](LICENSE) | [Console examples](tests/Mrbr.Cryptography.TestConsoleApp/Program.cs) | [Project overview](docs/blog-project-overview.md) | [Development checklist](docs/future-development-checklist.md)

## Current scope

| Project | Public API | Purpose |
| --- | --- | --- |
| [Mrbr.Cryptography](src/Mrbr.Cryptography) | `ShamirSharedSecret` | Compact GF(256) sharing and reconstruction; no package dependencies |
| [Mrbr.Cryptography.Vss](src/Mrbr.Cryptography.Vss) | `PedersenVss` | Share creation, commitment-based verification and verified reconstruction |
| [Mrbr.Cryptography.Vss.Authentication](src/Mrbr.Cryptography.Vss.Authentication) | `AuthenticatedCommitmentPacket` | Ed25519 signatures binding commitments, public context and generation |
| [Mrbr.Cryptography.KeyManager](src/Mrbr.Cryptography.KeyManager) | `ShamirKeySharing` | Optional adapter for sharing handles or replayed key material |
| [Mrbr.Cryptography.Vss.KeyManager](src/Mrbr.Cryptography.Vss.KeyManager) | `AuthenticatedKeySharing` | Optional authenticated AES key recovery adapter |

The core accepts arbitrary byte sequences, including empty input and zero bytes.
Supported counts satisfy `2 <= threshold <= shareCount <= 255`. Any qualifying
quorum of genuine shares from the same sharing can recover the original bytes.
The VSS and authentication layers have their own projects and formats, leaving
the raw Shamir implementation independent of them. They use libsodium 1.0.22.

Potential uses include backup-key recovery, controlled emergency access and
recovery of keys for encrypted documents or images. Storage, custodian identity,
share release and authorisation belong to the application. Large content can be
encrypted separately and its key shared; current sharing APIs use contiguous
buffers, with authenticated streaming/chunk processing left as future work.

## Build and run

Install the .NET SDK pinned in [global.json](global.json):
`11.0.100-rc.3.26522.148`. The current library target is `net11.0`.
Initial restore requires NuGet access. Windows x64 is the exercised platform;
other platforms, trimming and NativeAOT still require validation.

From the repository root:

```powershell
dotnet build Mrbr.Cryptography.slnx --configuration Release
dotnet test --solution Mrbr.Cryptography.slnx --configuration Release
dotnet run --project tests/Mrbr.Cryptography.TestConsoleApp/Mrbr.Cryptography.TestConsoleApp.csproj --configuration Release
```

The normal solution builds without `Comparison/` or the separate manager
repositories. The console demonstrates short text and longer lorem ipsum using
all three sharing variants, each with a two-of-three threshold. It prints every
successful pair, the expected one-share failure, VSS share-tampering rejection,
and authenticated packet/wrong-public-key rejection. It waits for Enter before
closing.

## Use project references

NuGet packaging is on hold. Add a reference to the required library project in
your .NET 11 application, adjusting the path for your directory layout:

```xml
<ItemGroup>
  <ProjectReference Include="../Mrbr.Cryptography/src/Mrbr.Cryptography/Mrbr.Cryptography.csproj" />
</ItemGroup>
```

Use the VSS or authentication project instead when those capabilities are needed.
The optional manager adapters additionally require their separate service projects.

## Public API and share layout

The raw Shamir API writes into caller-owned spans:

```csharp
int GetShareLength(int secretLength);
int GetSharesLength(int secretLength, int shareCount);
int CreateShares(ReadOnlySpan<byte> secret, int threshold, int shareCount, Span<byte> destination);
int Reconstruct(ReadOnlySpan<byte> shares, int shareLength, int threshold, Span<byte> destination);
```

Each raw share is `[coordinate | payload]`, with one payload byte per secret byte.
For `L` secret bytes, one share is `L + 1` bytes. Shares are packed consecutively;
reconstruction accepts complete shares in any order. Keep the original threshold
and share length separately, and pass only populated bytes rather than spare
capacity. All supplied shares participate.

### Example: recover with shares 1 and 3

```csharp
using System.Security.Cryptography;
using System.Text;
using Mrbr.Cryptography;

ReadOnlySpan<byte> secret = "Hello, Shamir!"u8;
const int threshold = 2, shareCount = 3;
int shareLength = ShamirSharedSecret.GetShareLength(secret.Length);
Span<byte> shares = stackalloc byte[ShamirSharedSecret.GetSharesLength(secret.Length, shareCount)];
Span<byte> selected = stackalloc byte[threshold * shareLength];
Span<byte> recovered = stackalloc byte[secret.Length];

try
{
    ShamirSharedSecret.CreateShares(secret, threshold, shareCount, shares);
    shares[..shareLength].CopyTo(selected); // Share 1.
    shares.Slice(2 * shareLength, shareLength).CopyTo(selected[shareLength..]); // Share 3.

    int written = ShamirSharedSecret.Reconstruct(selected, shareLength, threshold, recovered);
    Console.WriteLine(Encoding.UTF8.GetString(recovered[..written]));
}
finally
{
    CryptographicOperations.ZeroMemory(recovered);
    CryptographicOperations.ZeroMemory(selected);
    CryptographicOperations.ZeroMemory(shares);
}
```

These stack buffers are deliberately small. Reuse appropriately sized caller
buffers for large inputs. The [detailed API and mathematics guide](docs/shamir-shared-secret.md)
explains sizing, validation, cleanup and interpolation. The
[console source](tests/Mrbr.Cryptography.TestConsoleApp/Program.cs) includes the
corresponding VSS and authenticated examples.

## Verification and authentication

Raw Shamir shares are unauthenticated. Wrong thresholds, mixed sets and
structurally valid modified payloads can produce incorrect output without an
error. Use an appropriate authenticated workflow when accepting untrusted shares.

Pedersen verification checks consistency with a common commitment packet. The
application must establish one authenticated packet for all recipients. The
Ed25519 layer authenticates that packet against an independently trusted public
key and caller-supplied expected context and exact generation. It does not supply
an authorisation policy or automatic replay state. Keep shares private; signatures
do not encrypt public metadata. A valid dealer can still commit to unintended data.

- [Pedersen mathematics, protocol and API](docs/pedersen-vss.md)
- [Ed25519 packet format and trust boundaries](docs/authenticated-commitment-packets.md)
- [Authenticated AES key recovery](docs/authenticated-key-recovery.md)

The implementation has not undergone independent cryptographic review. Generated
code and timing behaviour remain platform/runtime dependent.

## Performance and allocation scope

The documented warmed operations use caller-owned spans and allocate zero managed
bytes on their measured paths. Caller buffers, initialisation, text conversion,
console output and exception paths are outside that claim. Native allocations in
the libsodium-based layers have not been measured.

The raw implementation uses SIMD with scalar fallback and optimised public-coordinate
interpolation weights. VSS verification/reconstruction optimisations remain separate.
The [benchmark guide](benchmarks/README.md) and
[performance article](docs/blog-performance-and-benchmarks.md) link complete
measurements, allocations, baselines and uncertainty. The runtime-only and
retargeting controls gave mixed results; implementation comparisons do not assign
their whole performance difference to .NET 11.

## Optional manager integration

The integration solution requires separate `Mrbr.Service.KeyManager` and
`Mrbr.Service.EncryptionManager` checkouts. Their parent directory is configurable:

```powershell
./eng/Invoke-Integration.ps1 -Action Test -Configuration Release -ServiceSourceRoot C:/dev/source
```

See [manager integration](docs/key-manager-integration.md) for the required layout
and [ServiceProjects.props](ServiceProjects.props) for the override. These adapters
are optional; the standalone solution does not require the manager repositories.

## External comparison source

`Comparison/` is a local-only checkout used to benchmark SecretSharingDotNet and
is excluded from Git. Downloaded third-party source/documentation snapshots and
frozen upstream assemblies in benchmark provenance are excluded too. Curated
results, JSON hash manifests and our own benchmark harness snapshots remain included.

The comparison harnesses are outside the normal solution and require the upstream
source separately. See [comparison setup](docs/comparison-setup.md) and the
[recorded results](benchmarks/results/2026-11-04-secretsharingdotnet/README.md).
Third-party licences remain applicable to their own projects and dependencies.

## Documentation and development

- [Project description and possible uses](docs/blog-project-overview.md)
- [Performance optimisations and SecretSharingDotNet benchmarks](docs/blog-performance-and-benchmarks.md)
- [Raw Shamir API, mathematics and verification](docs/shamir-shared-secret.md)
- [Interpolation-weight optimisation](docs/interpolation-weights.md)
- [Pedersen verification optimisation](docs/vss-verification-optimization.md)
- [Future-development notes](docs/future-development.md)
- [Hierarchical functionality checklist](docs/future-development-checklist.md)

The proposed self-describing raw-share envelope is not implemented. Its planned
component is separate, optional and off by default. Share refresh, resharing and
role-specific custody protocols are also future design work.

## Licence

Mrbr.Cryptography's original source and documentation are available under the
[MIT licence](LICENSE). Dependencies retain their own terms; see
[third-party notices](THIRD-PARTY-NOTICES.md).
