# Future development

Current preference: consume the libraries through project references while the
application and derived test projects evolve. NuGet packaging is on hold; no
package publication or packaging work is authorized by this document.

The existing libraries support the current sharing, VSS verification, signed
commitment and authenticated AES recovery workflows. The items below are future
options, not prerequisites for using those APIs.

The [hierarchical functionality checklist](future-development-checklist.md)
records completion through stable `FD` references. Every checklist reference
links back to its source section here. Checked implementation entries have
supporting code, documentation or recorded validation; proposed work remains
unchecked. Requirements/documentation and implementation are tracked separately.

| Reference group | Functionality |
| --- | --- |
| [FD.A](future-development-checklist.md#fd-a) | Existing sharing and authentication foundation |
| [FD.B](future-development-checklist.md#fd-b) | Application integration and interoperability |
| [FD.C](future-development-checklist.md#fd-c) | Optional raw-share envelope |
| [FD.D](future-development-checklist.md#fd-d) | Long-term document recovery application |
| [FD.E](future-development-checklist.md#fd-e) | Deployment, assurance and automation |
| [FD.F](future-development-checklist.md#fd-f) | Optional cryptographic/workflow extensions |
| [FD.G](future-development-checklist.md#fd-g) | Performance research and comparisons |
| [FD.H](future-development-checklist.md#fd-h) | Project references and deferred distribution |

## Existing foundation

The implemented foundation comprises data-agnostic raw Shamir sharing, GF(256)
arithmetic, SIMD and small-quorum interpolation optimization; separate Pedersen
VSS creation/verification/reconstruction; and separate Ed25519 commitment
signing/authentication with caller-supplied trust and expected context/generation.
The [project overview](../README.md#current-scope) and linked guides describe these
APIs, their tests and measured warmed zero-managed-allocation boundaries. These
completed primitives are recorded separately from optional extensions below.

## Application integration

Existing optional adapters provide raw handle/key sharing and
[authenticated AES recovery](authenticated-key-recovery.md), including decryption
without the original KeyManager source. Current guides and the separately
maintained [console examples](../tests/Mrbr.Cryptography.TestConsoleApp/Program.cs)
demonstrate independent use of all three variants. Earlier scratch examples under
`BenchmarkDotNet.Artifacts` remain local-only. Dedicated maintained sample projects
separate from the user's TestConsole remain future work. Current API/wire descriptions and
internal reference vectors exist; a formal compatibility policy and independently
consumable interoperability-vector suite remain outstanding.

- Add small consumer projects using project references to demonstrate independent
  use of raw Shamir, Pedersen VSS and signed commitments without either manager.
  Keep the user's maintained TestConsole separate.
- Establish an API and wire-format compatibility/versioning policy and publish
  fixed interoperability vectors for applications implemented independently.
- Consider a separate optional self-describing envelope for raw Shamir shares,
  off by default and selected explicitly through a separate component:
  version, algorithm/field identifier, random share-set ID, threshold, issued share
  count, original payload length, coordinate and raw payload. Keep the current
  compact sharing APIs available. Specify canonical parsing, length limits and
  header authentication separately; metadata alone does not prove authenticity.
- Consider reconstruction from separately stored share buffers and batch share
  verification returning caller-owned per-share results. Keep rejection/selection
  policy explicit, including the treatment of invalid surplus shares.
- Consider optional text/Base64 transport helpers in a separate layer, using
  caller buffers and explicit sizing. Storage and transport I/O stay outside the
  cryptographic libraries.

## Optional envelope and zero-trust boundaries

This is a future design option, not an implemented format. It would belong in a
separate serialization/authentication project or an application's export/import
adapter. The raw sharing APIs would retain their current behavior and dependency
graph. Explicitly choosing an envelope API is preferable to a global switch or
automatic format detection in the sharing engine.

Potential uses include offline recovery exports, interchange between independent
applications, and long-lived backups whose original application database may no
longer exist. Applications already holding trusted share parameters in their own
manifest or database may gain little from repeating them in every share.

The metadata is a set of claims, not authorization or a source of trust:

- Apply fixed format/version allowlists, exact framing, checked arithmetic and
  caller-defined byte/work limits before expensive processing. An incoming length
  must never direct unbounded allocation or stack allocation. Preserve caller-owned
  buffers and test allocation behavior separately from the raw engine.
- Require application policy to approve the algorithm, threshold, identity and
  intended use. A valid signature still needs authorization checks. Obtain trusted
  verification keys and expected tenant/resource/context from independent trusted
  state; never promote an embedded key to a trust anchor or replay a KeyHandle
  merely because the incoming record names it.
- Authenticate the canonical header and its exact share, or bind the header to
  commitments against which the share is verified. A set ID or checksum is not
  authentication. The existing signed VSS protocol does not automatically
  authenticate a new raw-share envelope format.
- Enforce freshness using independent expected generation/recovery state. A
  correctly signed historical packet can still be a replay. Never fall back to
  unauthenticated/raw processing when envelope authentication fails.
- Minimize public metadata. Share-set IDs enable correlation; lengths, thresholds
  and counts expose recovery structure. Avoid names, storage locations, file paths
  and confidential context. Signatures provide no confidentiality for the header.
- Test malformed lengths, unsupported suites, header/payload tampering, substituted
  sets, stale generations, unauthorized signers and attempted downgrade/fallback.

Changing an advertised threshold cannot reduce the mathematical threshold of an
already-created sharing. Trusting the changed header could nevertheless cause a
consumer to bypass policy checks or accept invalid reconstruction output. Raw
Shamir does not authenticate reconstructed data.

These recommendations apply zero-trust principles to this proposed format; they
do not constitute a claim that a format alone implements a zero-trust architecture.
See [NIST SP 800-207](https://csrc.nist.gov/pubs/sp/800/207/final) for the
authentication/authorization boundary and
[OWASP input validation](https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html)
for allowlists, semantic checks and resource limits.

## Long-term document recovery scenario

Example application: an encrypted document, such as a will, retained in a
solicitor's digital vault, with recovery shares held by the solicitor and
executors. Staff turnover, changes of custodian and document revisions make
documented recovery information valuable over a period of many years.

An application adapter could bind an opaque document identifier, document
revision, key generation and share-set identifier into authenticated metadata.
These are separate concepts: a new sharing of the same key does not necessarily
mean a new document revision, and a new key generation need not change the
envelope format version. Legal-document concepts remain outside the data-agnostic
sharing library.

A possible application workflow is to encrypt each document revision under a
fresh key, distribute that key's shares, and retain the encrypted artifact and
an authenticated recovery manifest in the vault. The manifest can bind the exact
encrypted artifact to the revision, key generation, share set and recovery policy.
Executor-held envelopes can identify the matching revision and share set without
containing document contents, personal names or vault storage locations.

Future design and validation must address:

- Independently authorised selection of the revision to recover. An authenticated
  envelope identifies an issued revision; it cannot establish that the revision
  is the latest or the one currently authorised. A trusted revision history and
  recovery process must survive loss of the current application database.
- Rejection of shares from different sharing instances, including separate
  issuances of the same secret. A plain set ID helps grouping but needs binding
  to the actual shares through the selected authentication/verification scheme.
- Explicit custody requirements. Ordinary k-of-n Shamir permits any k distinct
  shares from one set. Requiring solicitor participation plus a specified number
  of executors is a different access policy; role labels in a header cannot
  enforce it cryptographically and need a separate design if that is required.
- Revision and custodian changes. New metadata cannot revoke old shares or erase
  copied documents/keys. Retained old quorums can recover old keys; issuing a new
  set for the same key does not remove that capability. Fresh keys can protect
  subsequent revisions but do not withdraw access to historical copies.
- Institutional handover of trust records, historical verification keys,
  protected recovery records and documented procedures. Recovery must not depend
  on one employee's memory or personal account. Plan restoration exercises and
  reviewed migration of formats/algorithms for the intended retention period.

[NIST key-management guidance](https://csrc.nist.gov/pubs/sp/800/57/pt1/r5/final)
addresses archival and recovery of keying material and associated information.
The proposed workflow above is application design work, not a determination of
legal validity, document precedence or authority to disclose a document.

## Deployment and assurance

- Validate each intended OS/architecture and deployment mode. Windows x64 is
  currently exercised; Linux, macOS, ARM64, trimming and NativeAOT require their
  own validation before being advertised as supported configurations.
- Keep .NET 11 as the current target. Supporting earlier target frameworks is a
  separate compatibility decision, requiring performance and correctness checks.
- Arrange independent cryptographic review of the protocols, implementation,
  malformed-input behavior, secret cleanup and managed/native boundaries.
- Extend malformed-input fuzzing and published interoperability vectors where
  they add coverage beyond the existing deterministic/property/reference tests.
- Investigate generated machine code and timing behavior on supported platforms;
  zero allocations or fixed source-level loops are not universal timing guarantees.
- Add repeatable CI correctness, allocation and native-deployment checks for
  supported environments; benchmark regressions need controlled hardware and
  uncertainty reporting.

## Optional cryptographic extensions

- Share refresh or resharing while retaining a secret, with a defined protocol
  and authenticated epochs. These require separate design and review.
- Distributed complaint handling/agreement and dealer-equivocation handling if
  applications need a complete multi-party VSS protocol. The current local
  primitives require the caller to establish one authenticated common packet.
- Recovery/session helpers only where they clarify ownership and lifetime;
  preserve allocation boundaries and explicit trusted context/generation inputs.
- Chunked processing for large byte streams if required by applications, using
  caller buffers and authenticated ordering/length/finalization semantics. Keep
  file/network I/O separate.

## Performance research

Recorded [scalar](../benchmarks/results/2026-11-03-scalar/README.md),
[SIMD](../benchmarks/results/2026-11-03-simd/README.md),
[weight](../benchmarks/results/2026-11-03-weights/README.md), VSS, authentication and
manager-integration measurements exist. The
[external comparison](../benchmarks/results/2026-11-04-secretsharingdotnet/README.md)
separates implementation, target-framework compilation and runtime effects, with
source provenance, allocation checks and complete measurements. The two requested
repositories have a [functionality review](comparison-functionality.md), and
[blog notes](blog-comparison-notes.md) record supported conclusions. Broader
multi-launch/hardware runs and reproducibly fixed reconstruction fixtures for
small runtime-effect comparisons remain future validation work.

- Compare library choices, numeric backends, target-framework compilation and
  runtime versions separately. A cross-library speed ratio is not proof that
  .NET 11 alone caused the difference.
- Preserve external comparison source except for the requested .NET 11 upgrade
  and unavoidable compatibility changes. Do not transplant its algorithms into
  this solution as part of benchmarking.
- Retain source/runtime fingerprints, full measurements, correctness checks,
  allocation results, workload definitions and limitations for blog comparisons.

## Deferred distribution work

When explicitly requested later: versioned NuGet metadata/packages, dependency
and native-asset validation in a clean package-consuming application, licensing
and release documentation. Continue project-reference consumption for now.
