# Mrbr.Cryptography: sharing secrets with verification and authenticated recovery

Blog foundation, reflecting the project on **5 November 2026**. The descriptions
below distinguish implemented features from possible application designs and
future work. Repository links are relative to this document; replace them with
published repository links when preparing the article for publication.

Mrbr.Cryptography is a .NET 11 project for dividing sensitive data into shares
and recovering it when a chosen number of those shares come together. It starts
with a small, data-agnostic Shamir implementation, adds a separate Pedersen
verifiable secret sharing implementation, and provides an optional Ed25519 layer
for authenticating commitment packets. Its APIs operate on caller-owned memory
and perform no file, network or console I/O.

The project explores two related concerns: how to distribute the ability to
recover a secret, and how to implement that recovery with explicit trust
boundaries and low runtime overhead. Applications choose the functionality they
need, while retaining control of storage, distribution and authorisation.

## Why divide a secret?

Keeping a recovery key in one place makes that place critical to both access and
availability. Making complete copies improves availability, but gives every copy
holder the ability to recover the protected information alone.

Threshold sharing offers a different arrangement. In a two-of-three scheme,
three shares are issued and any two can reconstruct the original data. One share
is insufficient, and losing one share need not prevent recovery. The application
can choose a larger quorum where that better suits its recovery requirements.

These properties depend on how shares are held and released. Three shares stored
under one compromised account do not provide three independent custody boundaries.
The party creating the shares also initially has the secret; creating shares does
not erase that original access.

## Three capabilities, kept separate

| Component | Main capability | What the application supplies |
| --- | --- | --- |
| `ShamirSharedSecret` | Create shares and reconstruct arbitrary bytes from a quorum | Correct parameters and protection against accepting modified or unrelated shares |
| `PedersenVss` | Verify shares against common commitments before reconstructing | One authenticated commitment packet shared consistently with recipients |
| `AuthenticatedCommitmentPacket` | Authenticate those commitments with Ed25519, context and generation | A trusted public key, expected context, expected generation and release policy |

The raw Shamir project has no package dependencies. Pedersen VSS and Ed25519
authentication use the packaged native libsodium implementation through separate
projects. Adding verification or signatures to an application does not add those
dependencies or execution steps to the raw Shamir implementation.

### Shamir sharing: recover from a quorum

The core API accepts bytes. Text, keys, images, PDFs and other documents can all
be represented as bytes; the sharing algorithm does not inspect their formats.
The caller supplies the input and output spans and retains the recovery
parameters. Current counts satisfy `2 <= threshold <= shareCount <= 255`.

For a two-of-three example, each secret byte is the constant term of a random
line over a finite field:

```text
f(x) = s + a*x
```

Here, `s` is the secret byte and `a` is a fresh random field element. Shares contain
evaluations at distinct nonzero coordinates. Two points determine the line and
therefore its value at zero, which is `s`. Reconstruction uses Lagrange
interpolation. In the ideal scheme, independent uniformly random coefficients
ensure that fewer than the required shares reveal no information about the secret
value. The implementation obtains randomness from the platform cryptographic RNG.
The construction originates in [Adi Shamir's paper, *How to Share a Secret*](https://people.cs.pitt.edu/~litman/courses/cs2001/lectures/lecture-02-p612-shamir.pdf).

This implementation works byte by byte in GF(256). Addition is XOR;
multiplication uses polynomial reduction, rather than ordinary integer arithmetic
modulo 256. An input of `L` bytes produces raw shares of `L + 1` bytes each,
including a coordinate byte. Empty input and binary zero bytes are supported.

Raw shares carry no authentication. Structurally valid modified shares or a wrong
supplied threshold can produce incorrect output without an error. That boundary
is part of the [raw API contract](../README.md#public-api-and-share-layout).

### Pedersen VSS: check share consistency

Verifiable secret sharing adds a way for recipients to check their shares against
public commitments. This project follows the commitment-based approach described
in [Torben Pryds Pedersen's verifiable secret sharing paper](https://link.springer.com/chapter/10.1007/3-540-46766-1_9).

The implementation uses a sharing polynomial and a separate random blinding
polynomial. Its coefficient commitments let a recipient check that the private
values in a share are consistent with those commitments, without reconstructing
the secret. The current reconstruction API verifies every supplied share and
rejects the operation if any supplied share fails.

Pedersen VSS uses a different field and share format from the raw GF(256)
implementation. It uses the scalar field of the prime-order
[Ristretto255 group described in RFC 9496](https://www.rfc-editor.org/rfc/rfc9496.html#section-4),
encoding at most 31 data bytes into each scalar block. The two implementations'
share formats are not interchangeable.

Verification establishes consistency with the selected commitments. The
application still needs to establish whose commitments they are and ensure that
recipients use the same packet. A dealer can also share the wrong data
consistently. The [VSS guide](pedersen-vss.md) explains these protocol boundaries.

### Ed25519 authentication: establish the signing key

The authentication project signs the commitment packet together with public
application context and a generation number. It uses pure Ed25519, specified in
[RFC 8032](https://www.rfc-editor.org/rfc/rfc8032.html#section-5.1), through libsodium.
The signature covers the packet framing, context, generation and exact commitment
bytes. Pedersen verification then checks the private shares against those
authenticated commitments.

Recipients supply the trusted public key independently of the received packet.
They also supply their expected context and generation. A packet signed by an
unrelated key, or altered after signing, is rejected.

Generation binding supports a caller's freshness policy. The application must
retain the expected generation: an old signed packet can still verify when its
old generation is supplied. The library does not maintain a replay database.
Signatures provide no confidentiality for the public packet or its context.
Shares remain sensitive data and require protected storage and private distribution.

## Sharing data and sharing encryption keys

Applications can share an entire byte sequence directly. For large documents,
they can also encrypt the content and share its encryption key. This keeps the
shares small while allowing the application to store the encrypted content using
its existing storage system. Direct sharing creates approximately one payload's
worth of data per raw share; it is not a storage-compression technique.
The current APIs operate on contiguous buffers; file streaming and authenticated
chunk processing remain future work.

The optional KeyManager adapters already support sharing handles and actual key
material. A handle alone requires the original KeyManager configuration to replay
the key. Sharing the actual key allows recovery without that source configuration.

The implemented authenticated AES recovery workflow is:

1. Replay an AES key into caller-owned workspace.
2. Create Pedersen shares and sign the commitment packet with recovery metadata.
3. Authenticate the expected identity and generation, verify the shares and
   reconstruct the key from a quorum.
4. Use the recovered key with the separate raw-key AES-GCM decryptor to decrypt
   the original ciphertext and check its authentication tag.

The [integration example](authenticated-key-recovery.md) covers AES-128, AES-192
and AES-256. Its tests include recovery after the original KeyManager source has
been removed. Storage, transport, custodian identity and permission to release
shares remain application responsibilities. Existing access to the original
KeyManager can still replay its keys; sharing does not revoke that access.

The recovered key exists in memory at the recovery endpoint and must be protected
there. The same principle applies to private-key backup: reconstructing a signing
key for use is a different capability from a threshold-signature protocol that
signs without reconstructing the complete key.

## Possible applications

These are application designs that could use the primitives, rather than complete
products already supplied by this library.

| Application | Possible use of the project |
| --- | --- |
| Disaster recovery | Distribute shares of a backup decryption key across independently managed custodians or locations |
| Organisational emergency access | Require a quorum to recover a protected application secret during a controlled recovery procedure |
| Long-term document archives | Protect document encryption keys while retaining enough recovery information for future custodians |
| Encrypted document and image storage | Keep file-format handling and bulk encryption in the application, with threshold recovery of the relevant keys |
| Key migration and offline backup | Recover actual key material even when the original key-generation service or configuration is unavailable |

Thresholds balance availability and required cooperation. The surrounding
application needs to decide who may hold or release each share, how identities
are checked, and how enough independent shares remain available over time.

## A long-term archive example: a solicitor and executors

Consider an encrypted document retained in a solicitor's digital vault, with key
shares held by designated custodians. Over many years, employees can leave,
executors can change and the document can acquire revisions. Recovery knowledge
needs to survive those changes.

A future application could bind an opaque document identifier, document revision,
key generation and share-set identifier into an authenticated recovery manifest.
Custodian-held envelopes could identify the corresponding shares without exposing
the document contents or listing storage locations. These identifiers serve
different purposes: issuing new shares does not necessarily mean changing the
document, and changing the document does not mean changing the envelope format.

The proposed self-describing envelope for raw Shamir shares remains future work.
It would be separate, optional and off by default. The existing signed VSS packet
is implemented; a document-specific archive, manifest and revision-selection
workflow are not.

An authenticated envelope can identify an issued revision. Selecting the revision
currently authorised for recovery requires an independently trusted history and
process. Keeping the old quorum also keeps the ability to recover its old key;
new metadata cannot revoke already-held shares or documents.

Custody rules need equal care. Ordinary two-of-three Shamir permits any two
distinct shares from the same set. Requiring a solicitor plus a particular
executor quorum would need a separately designed access structure if the rule
must be enforced cryptographically. Role labels in metadata cannot enforce it.
The [future-development notes](future-development.md#long-term-document-recovery-scenario)
record these design questions without making claims about legal document validity
or precedence.

## Performance through explicit memory ownership

The library APIs use `ReadOnlySpan<byte>` and `Span<byte>` so callers can allocate
and reuse buffers. The raw implementation uses bounded stack scratch, SIMD
payload operations with scalar fallback, and specialised interpolation-weight
calculation for small quorums. Separate optimisation work targets Pedersen
verification and reconstruction.

The measured allocation claim is **zero managed allocations on the documented
warmed paths**. Initialisation, caller buffers, text conversion, console output
and exception paths are outside that claim. Native allocation traffic in the
libsodium-based layers has not been measured. Zero managed allocations also do
not establish constant-time execution or eliminate the need to protect memory.

For one recorded raw-Shamir case, a 32-byte secret with a three-of-five quorum,
the .NET 11 RC3 measurements were:

| Operation | Mean time, microseconds | Managed bytes per operation |
| --- | ---: | ---: |
| Create all five shares | 0.599 | 0 |
| Reconstruct from three shares | 0.0601 | 0 |

These results were recorded on 4 November 2026, on Windows x64 with an Intel
i7-10750H and SDK `11.0.100-rc.3.26522.148`. Cleanup was included. The runs used
one launch, 15 warmups and 12 measurements. The [complete report](../benchmarks/results/2026-11-04-secretsharingdotnet/README.md)
includes uncertainty, allocations, source fingerprints, comparison backends and
workload limitations. These raw-Shamir numbers do not include VSS or signatures
and should not be presented as large-file throughput measurements.

The comparison work separates implementation differences, target-framework
compilation and execution runtime. Retargeting and runtime changes produced
smaller, mixed results. The large library-to-library differences cannot be
attributed to .NET 11 alone, and Span/SIMD APIs are available on earlier modern
.NET versions. [Comparison-specific blog notes](blog-comparison-notes.md) provide
the details for a separate performance discussion.
The [companion performance article](blog-performance-and-benchmarks.md) develops
that discussion with optimisation explanations, allocations and the full runtime controls.

## Working examples and development status

The [console application](../tests/Mrbr.Cryptography.TestConsoleApp/Program.cs)
demonstrates the same short text and longer lorem ipsum input using all three
variants. Each uses two-of-three shares and shows recovery with shares 1 and 2,
1 and 3, and 2 and 3, followed by rejection of a one-share attempt. The VSS
examples reject a tampered share; the authenticated examples reject a tampered
commitment packet and an unrelated public key.

Run it from the repository root:

```powershell
dotnet run --project tests/Mrbr.Cryptography.TestConsoleApp/Mrbr.Cryptography.TestConsoleApp.csproj --configuration Release
```

Recorded validation covers independent arithmetic references, fixed vectors,
sparse and dense coordinates, malformed inputs, tampering, recovery and warmed
allocations. The recorded standalone suite contains 407 tests; the integration
suite contains 522. These are completed development checks, not an independent
cryptographic audit. Windows x64 is the exercised platform; additional operating
systems, ARM64, trimming and NativeAOT still require validation.

The project currently uses project references. NuGet packaging is deferred.
Further options include the raw-share envelope, published interoperability
vectors, additional platform validation, independent review, share refresh and
resharing, and authenticated chunk processing. The
[functionality checklist](future-development-checklist.md) distinguishes completed
work from those proposed extensions.
