# Future development functionality checklist

Status reviewed: **4 November 2026**. This checklist tracks the implemented
foundation and the options in [future-development.md](future-development.md).
Unchecked optional or deferred items are not approval to implement them.

- Each entry has a stable alphanumeric, dot-delimited reference, such as
  `FD.C.3.2`. Its reference links back to the relevant source section in
  [future-development.md](future-development.md); completed work also links to evidence.
- A checked leaf is complete for its stated scope. Documentation/requirements
  tasks explicitly say so; recording a design does not complete its implementation.
- A parent is checked **if and only if all its direct children are checked**.
  An unchecked parent may therefore contain completed children. No partial state is used.
- Keep existing references stable when adding work; assign a new unused child
  number rather than renumbering existing entries.
- Completion reflects existing code, tests and recorded measurements. Tests were
  not rerun for this documentation-only update. Platform and allocation claims
  retain the scope and limitations of the linked evidence.

The raw-share envelope remains **optional, separate and off by default**. Existing
signed VSS packets do not complete the proposed raw-envelope implementation.
NuGet work remains on hold. The user's TestConsole remains separately maintained.

<a id="fd-a"></a>

- [x] **[FD.A](future-development.md#existing-foundation)** Existing sharing and authentication foundation.
  - [x] **[FD.A.1](future-development.md#existing-foundation)** Raw Shamir sharing.
    - [x] **[FD.A.1.1](future-development.md#existing-foundation)** GF(256) arithmetic with independent-reference tests — [evidence](shamir-shared-secret.md#the-mathematics).
    - [x] **[FD.A.1.2](future-development.md#existing-foundation)** Create shares for arbitrary bytes, including empty inputs, through caller-owned spans — [evidence](../src/Mrbr.Cryptography/ShamirSharedSecret.cs).
    - [x] **[FD.A.1.3](future-development.md#existing-foundation)** Reconstruct from packed shares with explicit threshold and caller-owned output — [evidence](../src/Mrbr.Cryptography/ShamirSharedSecret.cs).
    - [x] **[FD.A.1.4](future-development.md#existing-foundation)** Validate parameters, framing, coordinates and overlap; use bounded scratch and clear secret scratch — [evidence](shamir-shared-secret.md#implementation-and-verification).
    - [x] **[FD.A.1.5](future-development.md#existing-foundation)** Use 128-bit/256-bit SIMD with automatic selection and scalar fallback — [evidence](../src/Mrbr.Cryptography/Internal/Gf256Simd.cs).
    - [x] **[FD.A.1.6](future-development.md#existing-foundation)** Optimize public-coordinate interpolation weights for small quorums and test sparse/dense sets — [evidence](interpolation-weights.md).
    - [x] **[FD.A.1.7](future-development.md#existing-foundation)** Verify warmed zero-managed-allocation creation/reconstruction, including vector tails and RNG batches — [evidence](../tests/Mrbr.Cryptography.Tests).
  - [x] **[FD.A.2](future-development.md#existing-foundation)** Separate Pedersen VSS implementation.
    - [x] **[FD.A.2.1](future-development.md#existing-foundation)** Create data-agnostic shares and coefficient commitments over Ristretto255 — [evidence](pedersen-vss.md).
    - [x] **[FD.A.2.2](future-development.md#existing-foundation)** Verify an individual share against the selected commitment packet — [evidence](../src/Mrbr.Cryptography.Vss/PedersenVss.cs).
    - [x] **[FD.A.2.3](future-development.md#existing-foundation)** Verify every supplied share before reconstruction, rejecting invalid surplus shares — [evidence](../tests/Mrbr.Cryptography.Vss.Tests/PedersenVssTests.cs).
    - [x] **[FD.A.2.4](future-development.md#existing-foundation)** Bind shares to versioned commitments and their share-set identifier — [evidence](pedersen-vss.md).
    - [x] **[FD.A.2.5](future-development.md#existing-foundation)** Optimize small-quorum verification/reconstruction with tested fallbacks — [evidence](vss-verification-optimization.md).
    - [x] **[FD.A.2.6](future-development.md#existing-foundation)** Test independent arithmetic, tampering, sparse/dense coordinates and warmed allocation behavior — [evidence](../tests/Mrbr.Cryptography.Vss.Tests).
  - [x] **[FD.A.3](future-development.md#existing-foundation)** Separate Ed25519 commitment authentication.
    - [x] **[FD.A.3.1](future-development.md#existing-foundation)** Generate signing seeds and derive public verification keys into caller buffers — [evidence](../src/Mrbr.Cryptography.Vss.Authentication/AuthenticatedCommitmentPacket.cs).
    - [x] **[FD.A.3.2](future-development.md#existing-foundation)** Sign the exact commitment packet, public context and generation — [evidence](authenticated-commitment-packets.md).
    - [x] **[FD.A.3.3](future-development.md#existing-foundation)** Verify using an independently supplied trusted key, expected context and exact generation — [evidence](authenticated-commitment-packets.md).
    - [x] **[FD.A.3.4](future-development.md#existing-foundation)** Provide authenticated share verification and verified reconstruction — [evidence](../src/Mrbr.Cryptography.Vss.Authentication/AuthenticatedCommitmentPacket.cs).
    - [x] **[FD.A.3.5](future-development.md#existing-foundation)** Test published signature vectors, malformed packets, substitutions, tampering and warmed allocations — [evidence](../tests/Mrbr.Cryptography.Vss.Authentication.Tests).

<a id="fd-b"></a>

- [ ] **[FD.B](future-development.md#application-integration)** Application integration and interoperability.
  - [x] **[FD.B.1](future-development.md#application-integration)** Non-verifying KeyManager integration.
    - [x] **[FD.B.1.1](future-development.md#application-integration)** Share and reconstruct an encoded ulong handle — [evidence](../src/Mrbr.Cryptography.KeyManager/ShamirKeySharing.cs).
    - [x] **[FD.B.1.2](future-development.md#application-integration)** Replay actual key bytes into spans and share/reconstruct them — [evidence](key-manager-integration.md).
    - [x] **[FD.B.1.3](future-development.md#application-integration)** Support caller workspace for larger keys and wipe the used key workspace — [evidence](key-manager-integration.md).
    - [x] **[FD.B.1.4](future-development.md#application-integration)** Recover actual keys and decrypt existing ciphertext without the original KeyManager source — [evidence](key-manager-integration.md).
    - [x] **[FD.B.1.5](future-development.md#application-integration)** Validate manager integration and warmed allocation boundaries — [evidence](../tests/Mrbr.Cryptography.IntegrationTests).
  - [x] **[FD.B.2](future-development.md#application-integration)** Authenticated AES key recovery integration.
    - [x] **[FD.B.2.1](future-development.md#application-integration)** Replay and create signed Pedersen shares for AES-128/192/256 keys — [evidence](../src/Mrbr.Cryptography.Vss.KeyManager/AuthenticatedKeySharing.cs).
    - [x] **[FD.B.2.2](future-development.md#application-integration)** Bind the original handle, key length, application context and generation — [evidence](authenticated-key-recovery.md).
    - [x] **[FD.B.2.3](future-development.md#application-integration)** Recover with independently supplied expectations and no KeyManager source — [evidence](authenticated-key-recovery.md).
    - [x] **[FD.B.2.4](future-development.md#application-integration)** Decrypt the original AES-GCM artifact with the recovered key and original associated data — [evidence](authenticated-key-recovery.md).
    - [x] **[FD.B.2.5](future-development.md#application-integration)** Test end-to-end recovery, rejection behavior, workspace wiping and warmed allocations — [evidence](../tests/Mrbr.Cryptography.Vss.KeyManager.Tests).
  - [ ] **[FD.B.3](future-development.md#application-integration)** Independent consumer examples.
    - [x] **[FD.B.3.1](future-development.md#application-integration)** Document standalone raw Shamir usage — [evidence](../README.md#public-api-and-share-layout).
    - [x] **[FD.B.3.2](future-development.md#application-integration)** Document standalone Pedersen and signed-commitment usage — [evidence](authenticated-commitment-packets.md).
    - [x] **[FD.B.3.3](future-development.md#application-integration)** Provide a project-reference signed-VSS console consumer without either manager — [evidence](../tests/Mrbr.Cryptography.TestConsoleApp/Mrbr.Cryptography.TestConsoleApp.csproj).
    - [ ] **[FD.B.3.4](future-development.md#application-integration)** Add a maintained raw-Shamir consumer project outside generated artifact directories.
    - [ ] **[FD.B.3.5](future-development.md#application-integration)** Add a maintained Pedersen-only consumer project outside generated artifact directories.
    - [ ] **[FD.B.3.6](future-development.md#application-integration)** Add a maintained signed-commitment consumer project outside generated artifact directories.
  - [ ] **[FD.B.4](future-development.md#application-integration)** Compatibility policy and interoperability vectors.
    - [x] **[FD.B.4.1](future-development.md#application-integration)** Document the current binary layouts and public API contracts — [evidence](../README.md#public-api-and-share-layout).
    - [x] **[FD.B.4.2](future-development.md#application-integration)** Keep fixed arithmetic/signature vectors and independent reference checks in tests — [evidence](../tests).
    - [ ] **[FD.B.4.3](future-development.md#application-integration)** Establish the public API compatibility/versioning policy.
    - [ ] **[FD.B.4.4](future-development.md#application-integration)** Establish wire-format evolution, migration and compatibility rules.
    - [ ] **[FD.B.4.5](future-development.md#application-integration)** Publish standalone machine-readable raw Shamir interoperability vectors.
    - [ ] **[FD.B.4.6](future-development.md#application-integration)** Publish standalone machine-readable Pedersen VSS and signed-packet interoperability vectors.
    - [ ] **[FD.B.4.7](future-development.md#application-integration)** Validate the published vectors in an independently implemented consumer.
  - [ ] **[FD.B.5](future-development.md#application-integration)** Share-buffer and batch-verification APIs.
    - [x] **[FD.B.5.1](future-development.md#application-integration)** Support reconstruction from the existing contiguous packed-share buffer — [evidence](../src/Mrbr.Cryptography/ShamirSharedSecret.cs).
    - [ ] **[FD.B.5.2](future-development.md#application-integration)** Add reconstruction from separately stored share buffers without packing copies.
    - [x] **[FD.B.5.3](future-development.md#application-integration)** Expose individual VSS share verification — [evidence](../src/Mrbr.Cryptography.Vss/PedersenVss.cs).
    - [ ] **[FD.B.5.4](future-development.md#application-integration)** Return batch-verification results through caller-owned per-share output.
    - [x] **[FD.B.5.5](future-development.md#application-integration)** Document the current reject-invalid-surplus-shares behavior — [evidence](pedersen-vss.md).
    - [ ] **[FD.B.5.6](future-development.md#application-integration)** Define and validate explicit caller selection/rejection policies for future batch APIs.
  - [ ] **[FD.B.6](future-development.md#application-integration)** Optional text transport helpers.
    - [ ] **[FD.B.6.1](future-development.md#application-integration)** Define text/Base64 encodings and explicit sizing rules in a separate layer.
    - [ ] **[FD.B.6.2](future-development.md#application-integration)** Implement span-based text/Base64 writing and parsing.
    - [ ] **[FD.B.6.3](future-development.md#application-integration)** Test malformed encodings, short buffers, round trips and allocation behavior.

<a id="fd-c"></a>

- [ ] **[FD.C](future-development.md#optional-envelope-and-zero-trust-boundaries)** Optional raw-share envelope; off by default.
  - [ ] **[FD.C.1](future-development.md#optional-envelope-and-zero-trust-boundaries)** Component boundaries and default behavior.
    - [x] **[FD.C.1.1](future-development.md#optional-envelope-and-zero-trust-boundaries)** Record separate-component, explicit-selection and off-by-default requirements — [evidence](future-development.md#optional-envelope-and-zero-trust-boundaries).
    - [ ] **[FD.C.1.2](future-development.md#optional-envelope-and-zero-trust-boundaries)** Implement the optional component without changing raw sharing APIs or their dependencies.
    - [ ] **[FD.C.1.3](future-development.md#optional-envelope-and-zero-trust-boundaries)** Provide explicit envelope read/write APIs without automatic core format detection.
    - [ ] **[FD.C.1.4](future-development.md#optional-envelope-and-zero-trust-boundaries)** Verify that envelope adoption adds no work or allocations to existing raw operations.
  - [ ] **[FD.C.2](future-development.md#optional-envelope-and-zero-trust-boundaries)** Canonical binary format and span APIs.
    - [ ] **[FD.C.2.1](future-development.md#optional-envelope-and-zero-trust-boundaries)** Specify the version, algorithm/field identifiers and reserved-field rules.
    - [ ] **[FD.C.2.2](future-development.md#optional-envelope-and-zero-trust-boundaries)** Specify the share-set ID, threshold, issued count, original length, coordinate and payload.
    - [ ] **[FD.C.2.3](future-development.md#optional-envelope-and-zero-trust-boundaries)** Define exact framing, byte order, maximum sizes and overflow behavior.
    - [ ] **[FD.C.2.4](future-development.md#optional-envelope-and-zero-trust-boundaries)** Implement checked sizing and caller-buffer creation/parsing APIs.
    - [ ] **[FD.C.2.5](future-development.md#optional-envelope-and-zero-trust-boundaries)** Preserve warmed zero-managed-allocation success and untrusted-input rejection paths.
  - [ ] **[FD.C.3](future-development.md#optional-envelope-and-zero-trust-boundaries)** Authentication, policy and freshness.
    - [x] **[FD.C.3.1](future-development.md#optional-envelope-and-zero-trust-boundaries)** Document that metadata claims do not establish authenticity, authorization or freshness — [evidence](future-development.md#optional-envelope-and-zero-trust-boundaries).
    - [ ] **[FD.C.3.2](future-development.md#optional-envelope-and-zero-trust-boundaries)** Select and implement authentication binding all metadata to the exact share or verified commitments.
    - [ ] **[FD.C.3.3](future-development.md#optional-envelope-and-zero-trust-boundaries)** Require independently trusted verification keys and expected identity/context.
    - [ ] **[FD.C.3.4](future-development.md#optional-envelope-and-zero-trust-boundaries)** Enforce caller-approved algorithms, parameters and byte/work limits.
    - [ ] **[FD.C.3.5](future-development.md#optional-envelope-and-zero-trust-boundaries)** Compare independent expected generations/recovery state to reject stale packets.
    - [ ] **[FD.C.3.6](future-development.md#optional-envelope-and-zero-trust-boundaries)** Reject authentication failures without raw or unauthenticated fallback.
    - [ ] **[FD.C.3.7](future-development.md#optional-envelope-and-zero-trust-boundaries)** Define and enforce minimal public metadata, excluding confidential context and storage locations.
  - [ ] **[FD.C.4](future-development.md#optional-envelope-and-zero-trust-boundaries)** Envelope validation and performance.
    - [ ] **[FD.C.4.1](future-development.md#optional-envelope-and-zero-trust-boundaries)** Test truncation, oversized lengths, integer overflow and unsupported formats.
    - [ ] **[FD.C.4.2](future-development.md#optional-envelope-and-zero-trust-boundaries)** Test header/payload tampering, substitution and mixed share sets.
    - [ ] **[FD.C.4.3](future-development.md#optional-envelope-and-zero-trust-boundaries)** Test stale generations, wrong identities and untrusted verification keys.
    - [ ] **[FD.C.4.4](future-development.md#optional-envelope-and-zero-trust-boundaries)** Test algorithm downgrade and attempted fallback after failed authentication.
    - [ ] **[FD.C.4.5](future-development.md#optional-envelope-and-zero-trust-boundaries)** Measure throughput and managed allocations separately from raw Shamir benchmarks.

<a id="fd-d"></a>

- [ ] **[FD.D](future-development.md#long-term-document-recovery-scenario)** Long-term document recovery application.
  - [x] **[FD.D.1](future-development.md#long-term-document-recovery-scenario)** Scenario and requirements.
    - [x] **[FD.D.1.1](future-development.md#long-term-document-recovery-scenario)** Record the solicitor/executor vault scenario and staff-turnover requirements — [evidence](future-development.md#long-term-document-recovery-scenario).
    - [x] **[FD.D.1.2](future-development.md#long-term-document-recovery-scenario)** Distinguish envelope format, document revision, key generation and share-set identity — [evidence](future-development.md#long-term-document-recovery-scenario).
    - [x] **[FD.D.1.3](future-development.md#long-term-document-recovery-scenario)** Keep legal-document concepts in the application layer and preserve a data-agnostic library — [evidence](future-development.md#long-term-document-recovery-scenario).
  - [ ] **[FD.D.2](future-development.md#long-term-document-recovery-scenario)** Application records and recovery workflow.
    - [ ] **[FD.D.2.1](future-development.md#long-term-document-recovery-scenario)** Specify canonical metadata using opaque document and revision identifiers.
    - [ ] **[FD.D.2.2](future-development.md#long-term-document-recovery-scenario)** Implement document revision encryption under fresh revision keys in the application.
    - [ ] **[FD.D.2.3](future-development.md#long-term-document-recovery-scenario)** Bind the exact encrypted artifact, revision, key generation and share set in an authenticated manifest.
    - [ ] **[FD.D.2.4](future-development.md#long-term-document-recovery-scenario)** Produce and import executor-held envelopes through the optional component.
    - [ ] **[FD.D.2.5](future-development.md#long-term-document-recovery-scenario)** Recover the selected artifact using authenticated compatible shares and retained cipher metadata.
    - [ ] **[FD.D.2.6](future-development.md#long-term-document-recovery-scenario)** Validate complete recovery without the original KeyManager source or current application database.
  - [ ] **[FD.D.3](future-development.md#long-term-document-recovery-scenario)** Revision selection and historical recovery.
    - [x] **[FD.D.3.1](future-development.md#long-term-document-recovery-scenario)** Document that authenticated historical packets need not identify the currently authorised revision — [evidence](future-development.md#long-term-document-recovery-scenario).
    - [ ] **[FD.D.3.2](future-development.md#long-term-document-recovery-scenario)** Implement independently authorised revision selection and retained trusted revision history.
    - [ ] **[FD.D.3.3](future-development.md#long-term-document-recovery-scenario)** Reject shares from unrelated revisions, generations or sharing instances.
    - [ ] **[FD.D.3.4](future-development.md#long-term-document-recovery-scenario)** Test authorised historical recovery and rejection of unwanted rollback/replay.
  - [ ] **[FD.D.4](future-development.md#long-term-document-recovery-scenario)** Role-specific custody policy.
    - [x] **[FD.D.4.1](future-development.md#long-term-document-recovery-scenario)** Document that ordinary k-of-n Shamir allows any qualifying quorum — [evidence](future-development.md#long-term-document-recovery-scenario).
    - [ ] **[FD.D.4.2](future-development.md#long-term-document-recovery-scenario)** Define and review any requirement for solicitor participation plus an executor quorum.
    - [ ] **[FD.D.4.3](future-development.md#long-term-document-recovery-scenario)** Design a cryptographic access structure for the approved role policy, if required.
    - [ ] **[FD.D.4.4](future-development.md#long-term-document-recovery-scenario)** Implement and test that structure separately from the current k-of-n primitive.
  - [ ] **[FD.D.5](future-development.md#long-term-document-recovery-scenario)** Custodian changes and institutional continuity.
    - [x] **[FD.D.5.1](future-development.md#long-term-document-recovery-scenario)** Document that new metadata/shares cannot revoke retained old quorums, keys or documents — [evidence](future-development.md#long-term-document-recovery-scenario).
    - [ ] **[FD.D.5.2](future-development.md#long-term-document-recovery-scenario)** Define and implement custodian replacement and fresh-key/re-encryption procedures.
    - [ ] **[FD.D.5.3](future-development.md#long-term-document-recovery-scenario)** Preserve institutional trust records and historical verification keys independently of personal accounts.
    - [ ] **[FD.D.5.4](future-development.md#long-term-document-recovery-scenario)** Create protected recovery records and a handover/restoration runbook.
    - [ ] **[FD.D.5.5](future-development.md#long-term-document-recovery-scenario)** Conduct recovery exercises covering staff departure and unavailable custodians.
    - [ ] **[FD.D.5.6](future-development.md#long-term-document-recovery-scenario)** Define and exercise reviewed format/algorithm migration for the retention period.

<a id="fd-e"></a>

- [ ] **[FD.E](future-development.md#deployment-and-assurance)** Deployment, assurance and automation.
  - [ ] **[FD.E.1](future-development.md#deployment-and-assurance)** Platform and deployment validation.
    - [x] **[FD.E.1.1](future-development.md#deployment-and-assurance)** Exercise Windows x64 functional and warmed-allocation tests in Debug and Release — [evidence](shamir-shared-secret.md#implementation-and-verification).
    - [ ] **[FD.E.1.2](future-development.md#deployment-and-assurance)** Validate Linux deployment, native dependency loading, correctness and allocations.
    - [ ] **[FD.E.1.3](future-development.md#deployment-and-assurance)** Validate macOS deployment, native dependency loading, correctness and allocations.
    - [ ] **[FD.E.1.4](future-development.md#deployment-and-assurance)** Execute ARM64 correctness, SIMD dispatch and allocation checks.
    - [ ] **[FD.E.1.5](future-development.md#deployment-and-assurance)** Validate trimming in a consuming application.
    - [ ] **[FD.E.1.6](future-development.md#deployment-and-assurance)** Validate NativeAOT in a consuming application.
  - [ ] **[FD.E.2](future-development.md#deployment-and-assurance)** Target framework decisions.
    - [x] **[FD.E.2.1](future-development.md#deployment-and-assurance)** Target .NET 11 and pin the installed RC SDK — [evidence](../global.json).
    - [ ] **[FD.E.2.2](future-development.md#deployment-and-assurance)** Decide whether earlier target frameworks are required.
    - [ ] **[FD.E.2.3](future-development.md#deployment-and-assurance)** Implement any subsequently approved earlier targets.
    - [ ] **[FD.E.2.4](future-development.md#deployment-and-assurance)** Validate correctness, native deployment and performance for each additional target.
  - [ ] **[FD.E.3](future-development.md#deployment-and-assurance)** Independent review and robustness coverage.
    - [x] **[FD.E.3.1](future-development.md#deployment-and-assurance)** Maintain deterministic arithmetic/reference and malformed-input regression tests — [evidence](../tests).
    - [ ] **[FD.E.3.2](future-development.md#deployment-and-assurance)** Obtain independent review of protocol construction and trust boundaries.
    - [ ] **[FD.E.3.3](future-development.md#deployment-and-assurance)** Obtain independent review of malformed-input handling, cleanup and managed/native boundaries.
    - [ ] **[FD.E.3.4](future-development.md#deployment-and-assurance)** Add coverage-guided malformed-input fuzzing beyond deterministic regression cases.
  - [ ] **[FD.E.4](future-development.md#deployment-and-assurance)** Generated code and timing investigation.
    - [x] **[FD.E.4.1](future-development.md#deployment-and-assurance)** Inspect optimized x64 code for expected SIMD shuffle instructions — [evidence](shamir-shared-secret.md#implementation-and-verification).
    - [x] **[FD.E.4.2](future-development.md#deployment-and-assurance)** Document the limits of source-level timing and zero-allocation claims — [evidence](shamir-shared-secret.md#implementation-and-verification).
    - [ ] **[FD.E.4.3](future-development.md#deployment-and-assurance)** Investigate timing leakage on each supported platform and relevant workload.
    - [ ] **[FD.E.4.4](future-development.md#deployment-and-assurance)** Inspect relevant generated code on additional supported architectures/runtimes.
  - [ ] **[FD.E.5](future-development.md#deployment-and-assurance)** Repeatable CI checks.
    - [x] **[FD.E.5.1](future-development.md#deployment-and-assurance)** Provide local repeatable test/integration invocation scripts — [evidence](../eng).
    - [ ] **[FD.E.5.2](future-development.md#deployment-and-assurance)** Add CI correctness checks for the supported build/platform matrix.
    - [ ] **[FD.E.5.3](future-development.md#deployment-and-assurance)** Add CI managed-allocation regression checks.
    - [ ] **[FD.E.5.4](future-development.md#deployment-and-assurance)** Add clean-consumer native deployment checks to CI.
    - [ ] **[FD.E.5.5](future-development.md#deployment-and-assurance)** Add controlled-hardware benchmark regression checks with uncertainty reporting.

<a id="fd-f"></a>

- [ ] **[FD.F](future-development.md#optional-cryptographic-extensions)** Optional cryptographic and workflow extensions.
  - [ ] **[FD.F.1](future-development.md#optional-cryptographic-extensions)** Share refresh while retaining the secret.
    - [ ] **[FD.F.1.1](future-development.md#optional-cryptographic-extensions)** Define and review refresh protocol, authentication and epoch semantics.
    - [ ] **[FD.F.1.2](future-development.md#optional-cryptographic-extensions)** Implement the reviewed refresh protocol in a separate component.
    - [ ] **[FD.F.1.3](future-development.md#optional-cryptographic-extensions)** Test correctness, malformed/hostile inputs and allocation/performance behavior.
  - [ ] **[FD.F.2](future-development.md#optional-cryptographic-extensions)** Resharing to a different quorum or participant set.
    - [ ] **[FD.F.2.1](future-development.md#optional-cryptographic-extensions)** Define and review the resharing protocol and authenticated epoch transition.
    - [ ] **[FD.F.2.2](future-development.md#optional-cryptographic-extensions)** Implement the reviewed resharing protocol in a separate component.
    - [ ] **[FD.F.2.3](future-development.md#optional-cryptographic-extensions)** Test retained-secret correctness, participant changes, failures and allocations.
  - [ ] **[FD.F.3](future-development.md#optional-cryptographic-extensions)** Distributed VSS protocol support.
    - [x] **[FD.F.3.1](future-development.md#optional-cryptographic-extensions)** Document common-packet agreement and dealer-equivocation boundaries of current primitives — [evidence](pedersen-vss.md).
    - [ ] **[FD.F.3.2](future-development.md#optional-cryptographic-extensions)** Design authenticated common-packet agreement between participants.
    - [ ] **[FD.F.3.3](future-development.md#optional-cryptographic-extensions)** Design complaint handling and dealer-equivocation resolution.
    - [ ] **[FD.F.3.4](future-development.md#optional-cryptographic-extensions)** Implement and validate the selected multi-party protocol separately from local verification.
  - [ ] **[FD.F.4](future-development.md#optional-cryptographic-extensions)** Additional recovery/session helpers.
    - [x] **[FD.F.4.1](future-development.md#optional-cryptographic-extensions)** Provide the current explicit-buffer authenticated AES recovery helper — [evidence](authenticated-key-recovery.md).
    - [ ] **[FD.F.4.2](future-development.md#optional-cryptographic-extensions)** Identify additional generic session helpers that clarify ownership and lifetime.
    - [ ] **[FD.F.4.3](future-development.md#optional-cryptographic-extensions)** Implement approved helpers with explicit expected context/generation and caller-owned state.
    - [ ] **[FD.F.4.4](future-development.md#optional-cryptographic-extensions)** Test state transitions, failure behavior, lifetime and allocations.
  - [ ] **[FD.F.5](future-development.md#optional-cryptographic-extensions)** Chunked processing for large data.
    - [ ] **[FD.F.5.1](future-development.md#optional-cryptographic-extensions)** Define authenticated ordering, total length and finalization semantics.
    - [ ] **[FD.F.5.2](future-development.md#optional-cryptographic-extensions)** Implement caller-buffer chunk processing with storage/network I/O outside the library.
    - [ ] **[FD.F.5.3](future-development.md#optional-cryptographic-extensions)** Test missing/reordered/replayed chunks, boundary sizes and end-to-end reconstruction.
    - [ ] **[FD.F.5.4](future-development.md#optional-cryptographic-extensions)** Measure large-payload throughput and allocations.

<a id="fd-g"></a>

- [ ] **[FD.G](future-development.md#performance-research)** Performance research and external comparisons.
  - [x] **[FD.G.1](future-development.md#performance-research)** Recorded internal optimization experiments.
    - [x] **[FD.G.1.1](future-development.md#performance-research)** Record scalar creation/reconstruction throughput and allocations — [evidence](../benchmarks/results/2026-11-03-scalar/README.md).
    - [x] **[FD.G.1.2](future-development.md#performance-research)** Compare SIMD against the scalar baseline — [evidence](../benchmarks/results/2026-11-03-simd/README.md).
    - [x] **[FD.G.1.3](future-development.md#performance-research)** Compare interpolation-weight optimizations against the SIMD baseline — [evidence](../benchmarks/results/2026-11-03-weights/README.md).
    - [x] **[FD.G.1.4](future-development.md#performance-research)** Measure Pedersen creation/verification/reconstruction and optimized small-quorum behavior — [evidence](../benchmarks/results/2026-11-03-vss-optimized/README.md).
    - [x] **[FD.G.1.5](future-development.md#performance-research)** Measure Ed25519 authentication and complete authenticated recovery overhead — [evidence](../benchmarks/results/2026-11-04-vss-authenticated/README.md).
    - [x] **[FD.G.1.6](future-development.md#performance-research)** Measure key replay/sharing/recovery and raw-key AES-GCM integration — [evidence](../benchmarks/results/2026-11-04-authenticated-recovery/README.md).
  - [x] **[FD.G.2](future-development.md#performance-research)** SecretSharingDotNet comparison and functionality review.
    - [x] **[FD.G.2.1](future-development.md#performance-research)** Pin upstream revisions and verify the supplied source copy against GitHub — [evidence](comparison-functionality.md).
    - [x] **[FD.G.2.2](future-development.md#performance-research)** Add upstream .NET 11 targets with only required project/lock-file changes — [evidence](../benchmarks/results/2026-11-04-secretsharingdotnet/provenance/final-integrity.json).
    - [x] **[FD.G.2.3](future-development.md#performance-research)** Validate both upstream numeric backends through public APIs and run the selected upstream tests — [evidence](../benchmarks/results/2026-11-04-secretsharingdotnet/metadata.json).
    - [x] **[FD.G.2.4](future-development.md#performance-research)** Measure both libraries on .NET 11, including throughput and managed allocations — [evidence](../benchmarks/results/2026-11-04-secretsharingdotnet/README.md).
    - [x] **[FD.G.2.5](future-development.md#performance-research)** Separate target-compilation effects using forced netstandard2.0/net10.0/net11.0 builds — [evidence](../benchmarks/results/2026-11-04-secretsharingdotnet/target-comparison.csv).
    - [x] **[FD.G.2.6](future-development.md#performance-research)** Separate runtime effects using the same frozen upstream DLL on .NET 10 and 11 — [evidence](../benchmarks/results/2026-11-04-secretsharingdotnet/runtime-comparison.csv).
    - [x] **[FD.G.2.7](future-development.md#performance-research)** Verify that comparison work does not change our production source or import upstream algorithms — [evidence](../benchmarks/results/2026-11-04-secretsharingdotnet/provenance/final-integrity.json).
    - [x] **[FD.G.2.8](future-development.md#performance-research)** Retain source/runtime hashes, workload definitions, uncertainty, allocations and complete results — [evidence](../benchmarks/results/2026-11-04-secretsharingdotnet/metadata.json).
    - [x] **[FD.G.2.9](future-development.md#performance-research)** Review functionality in both requested GitHub repositories — [evidence](comparison-functionality.md).
    - [x] **[FD.G.2.10](future-development.md#performance-research)** Prepare blog notes distinguishing implementation differences from runtime-version effects — [evidence](blog-comparison-notes.md).
  - [ ] **[FD.G.3](future-development.md#performance-research)** Broader performance evidence.
    - [x] **[FD.G.3.1](future-development.md#performance-research)** Record one-host results and the limits of one launch and per-process randomized reconstruction fixtures — [evidence](../benchmarks/results/2026-11-04-secretsharingdotnet/README.md).
    - [ ] **[FD.G.3.2](future-development.md#performance-research)** Repeat comparisons with multiple launches on intended deployment hardware.
    - [ ] **[FD.G.3.3](future-development.md#performance-research)** Use reproducibly fixed reconstruction fixtures when isolating small runtime changes.
    - [ ] **[FD.G.3.4](future-development.md#performance-research)** Compare additional supported architectures while retaining source/runtime provenance.

<a id="fd-h"></a>

- [ ] **[FD.H](future-development.md#deferred-distribution-work)** Project-reference workflow and deferred distribution.
  - [x] **[FD.H.1](future-development.md#deferred-distribution-work)** Current development workflow.
    - [x] **[FD.H.1.1](future-development.md#deferred-distribution-work)** Keep NuGet packaging/publication on hold — [evidence](future-development.md#deferred-distribution-work).
    - [x] **[FD.H.1.2](future-development.md#deferred-distribution-work)** Use project references for local libraries and integration work — [evidence](../Mrbr.Cryptography.Integration.slnx).
    - [x] **[FD.H.1.3](future-development.md#deferred-distribution-work)** Preserve the separately maintained TestConsole boundary — [evidence](../README.md#current-scope).
  - [ ] **[FD.H.2](future-development.md#deferred-distribution-work)** NuGet/release work; deferred until explicitly requested.
    - [ ] **[FD.H.2.1](future-development.md#deferred-distribution-work)** Define release versioning and package metadata.
    - [ ] **[FD.H.2.2](future-development.md#deferred-distribution-work)** Complete package licensing and dependency/native-asset declarations.
    - [ ] **[FD.H.2.3](future-development.md#deferred-distribution-work)** Create versioned packages.
    - [ ] **[FD.H.2.4](future-development.md#deferred-distribution-work)** Validate clean package-consuming applications, including native dependency deployment.
    - [ ] **[FD.H.2.5](future-development.md#deferred-distribution-work)** Prepare release and package-consumption documentation.
    - [ ] **[FD.H.2.6](future-development.md#deferred-distribution-work)** Publish approved packages only after explicit publication authorization.
