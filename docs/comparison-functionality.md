# Sharing-library functionality review

Reviewed 4 November 2026. Performance experiments and their limitations are in
the [benchmark report](../benchmarks/results/2026-11-04-secretsharingdotnet/README.md).
This is an API/source functionality review, not an independent security audit.
No external sharing algorithm is incorporated into Mrbr.Cryptography.

## Revisions and changes

- SecretSharingDotNet main: `bb0405a2bc954dc5a1c9fa77611e9969902cee1e`, dated
  26 September 2026, version 1.1.0. All 150 files of the supplied local copy
  matched the GitHub commit's Git blob hashes exactly before modification.
- ShamirSecretSharing development: `8ab77f00b87310d3a282d367dc57457ea9bc1fbd`,
  dated 2 November 2026. Source/docs were retrieved for review; it is not the
  subject of the measured performance experiments.
- The GitHub API reported 50 and 4 stars respectively at retrieval. Those figures
  do not establish a GitHub-wide popularity ranking. A blog should identify the
  selected project/revision rather than call it the most popular without a
  separately defined survey.

Only the requested .NET 11 upgrade was made inside the local SecretSharingDotNet
copy: add net11.0 to the library and test targets, include net11.0 in the tests'
existing CsCheck package condition, and retain generated net11 lock-file entries.
Existing target dependency entries and all C# files are unchanged. The two project
files and two package-lock files are the complete change set. No C# compatibility
fix was required. SourceLink emits archive-related warnings because the downloaded
copy has no Git working directory.

## Capabilities

| Area | Mrbr.Cryptography family | SecretSharingDotNet 1.1.0 | ShamirSecretSharing current development |
| --- | --- | --- | --- |
| Basic field/model | GF(256), independent byte polynomials | Entire encoded secret as a number modulo a selected Mersenne prime | Byte-wise GF(p), default p=257 |
| Binary input | Arbitrary byte spans, including empty | Nonempty byte input through Secret<TNumber> | Nonempty byte span input; array/string facade |
| Share count | Up to 255 | Implementation ceiling 1,000,000, also bounded by field/resources | Up to prime−1; 256 at default prime |
| Numeric/text helpers | Caller can encode any value into bytes; dedicated handle/key adapters | BigInteger/SecureBigInteger, pinned text, UTF-8/Base64 and numeric conversions | Byte/string helpers and configurable encoding |
| Memory/API style | Caller-owned input/output spans; warm zero managed allocations | Object/result ownership, IDisposable, pinned pooled buffers; allocating arithmetic/results | Allocated Share[] and int[] values; some stack/span scratch |
| Serialized share metadata | Compact raw Shamir; separate versioned VSS packet and signed envelope | Default legacy INDEX-VALUE; opt-in extended INDEX-VALUE-LEVEL | Hex share text; additional authenticated-share text format |
| Verifiable sharing | Separate Pedersen/Ristretto255 implementation | No Pedersen VSS or per-share integrity scheme in this revision | Optional HMAC-authenticated wrapper; no Pedersen commitment construction |
| Dealer identity/context | Separate Ed25519 signed commitments with trusted key, expected context/generation | Not supplied by the basic sharing protocol | Quorum-time HMAC verification with a share-carried key; different trust model |
| Side-channel scope | Avoids secret-indexed memory tables in core; SIMD/register shuffles; no universal managed constant-time claim | BigInteger is variable time; SecureBigInteger + fixed-iteration inverse reduces timing leakage with documented limits | Finite-field arithmetic has no universal constant-time guarantee |
| Integration scope | Optional actual AES key replay/recovery and existing raw-key decryption | Generic numeric backends, DI-friendly use-case interfaces, custom prime providers and GCD strategies | Separate splitter/combiner, injectable randomness and facade service |
| Targets | net11.0 | Standard 2.0/2.1, Framework 4.7.2/4.8/4.8.1, .NET 8/9/10; net11 added locally | .NET 8/9 in the reviewed project |

These represent different workloads and ownership/security choices. Matching a
payload length and quorum does not make their internal arithmetic or cleanup
work identical. VSS/Ed25519 are not enabled in the raw-Shamir performance contest.

The SecretSharingDotNet details are supported by its
[README](https://github.com/shinji-san/SecretSharingDotNet/blob/bb0405a2bc954dc5a1c9fa77611e9969902cee1e/README.md),
[changelog](https://github.com/shinji-san/SecretSharingDotNet/blob/bb0405a2bc954dc5a1c9fa77611e9969902cee1e/CHANGELOG.md)
and public [splitter](https://github.com/shinji-san/SecretSharingDotNet/blob/bb0405a2bc954dc5a1c9fa77611e9969902cee1e/src/Cryptography/ShamirsSecretSharing/SecretSplitter%601.cs).
The backend called SecureBigInteger does not make the complete operation
constant time: the project's own documentation describes intermediate limb-length
and per-iteration timing limits. The fixed-iteration reconstructor is used for
that backend in our measurements, as recommended upstream.

## The second repository's missing-feature statements

Its [README](https://github.com/stevehansen/ShamirSecretSharing/blob/8ab77f00b87310d3a282d367dc57457ea9bc1fbd/README.md)
describes the basic Shamir API, whose shares have no integrity protection. Its
older [STRIDE document](https://github.com/stevehansen/ShamirSecretSharing/blob/8ab77f00b87310d3a282d367dc57457ea9bc1fbd/STRIDE.md)
also describes missing authentication and a byte-modulo coefficient-sampling
bias. Those statements should not be treated as the complete current feature set.

The current [changelog](https://github.com/stevehansen/ShamirSecretSharing/blob/8ab77f00b87310d3a282d367dc57457ea9bc1fbd/CHANGELOG.md)
and implementation include:

- AuthenticatedSecretSplitter/AuthenticatedSecretCombiner: per-share HMAC-SHA256
  tags truncated to 16 bytes, an issue identifier and an issue key that is itself
  Shamir-shared. The combiner reconstructs that key and verifies shares before
  producing the payload. This is not our Pedersen/Ed25519 protocol.
- ShareAuthenticationException with offending-coordinate reporting where available.
- CryptoRandomSource.GetInts using RandomNumberGenerator.GetInt32, addressing the
  older modulo-bias description. Injectable random sources support deterministic tests.
- Separate splitter/combiner classes, a shared default field, and Miller–Rabin
  validation for custom prime moduli.
- Current raw share and authenticated share parsing/formatting.

See the current
[authenticated splitter](https://github.com/stevehansen/ShamirSecretSharing/blob/8ab77f00b87310d3a282d367dc57457ea9bc1fbd/ShamirSecretSharing/AuthenticatedSecretSplitter.cs),
[combiner](https://github.com/stevehansen/ShamirSecretSharing/blob/8ab77f00b87310d3a282d367dc57457ea9bc1fbd/ShamirSecretSharing/AuthenticatedSecretCombiner.cs)
and [random source](https://github.com/stevehansen/ShamirSecretSharing/blob/8ab77f00b87310d3a282d367dc57457ea9bc1fbd/ShamirSecretSharing/CryptoRandomSource.cs).
The raw API still has its stated basic-Shamir limitations. The additional wrapper
must be selected explicitly; it is not an automatic change to raw share semantics.

No performance or security-equivalence claim is made for that second project.
Its tests were not executed as part of the SecretSharingDotNet benchmark exercise.

## Self-describing raw shares

Our raw Shamir share currently contains only `[coordinate | payload]`. The caller
retains its threshold, payload length and grouping separately. A future optional
envelope could logically contain:

```text
version | algorithm/field | set ID | threshold | issued count |
original length | coordinate | share payload
```

This is a proposed schema, not an implemented wire layout. It would let a reader
understand stored shares without a sidecar description and reject accidental
mixing/unsupported formats. The header itself would remain untrusted unless an
authentication scheme covered it. A checksum or random set ID alone would not
establish dealer identity or prevent deliberate substitution.

Our existing VSS and authenticated commitment packets already carry versioned
parameters and identity/context binding. SecretSharingDotNet's opt-in extended
format records a field exponent; its documented default legacy format drops that
metadata. Neither fact warrants changing our arithmetic or wire formats during
this comparison. The [future-development list](future-development.md) keeps this
and the other suggestions separate from current implementation work.
