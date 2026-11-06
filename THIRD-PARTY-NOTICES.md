# Third-party notices

The [MIT licence](LICENSE) covers Mrbr.Cryptography's original source and
documentation. Dependencies and external benchmark subjects retain their own
licences. Referencing or benchmarking a project does not relicense its code.

## libsodium

`Mrbr.Cryptography.Vss` and `Mrbr.Cryptography.Vss.Authentication` reference the
`libsodium` NuGet package, version 1.0.22. The restored package declares the ISC
licence and supplies this notice. Its native binaries are restored dependencies,
not checked-in repository assets.

```text
ISC License

Copyright (c) 2013-2026
Frank Denis <j at pureftpd dot org>

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
```

Project: [libsodium](https://github.com/jedisct1/libsodium).
For redistribution of restored binaries, retain the notices supplied with the
actual package version being distributed.

## Test and benchmark dependencies

Test and benchmark projects restore their dependencies through NuGet, including
xUnit, Microsoft.Testing.Platform and BenchmarkDotNet. Their package licences and
notices remain applicable. This document is not an exhaustive transitive dependency
inventory; package metadata and included notices identify the corresponding terms.

## External comparison projects

The local `Comparison/` directory is ignored by Git. Copies of upstream source,
documentation and assemblies in the external-comparison provenance directory are
also ignored. JSON manifests, measured reports and our benchmark adapters are
retained. The adapters call public APIs; they do not incorporate upstream sharing
arithmetic into Mrbr.Cryptography.

The [comparison setup guide](docs/comparison-setup.md) identifies the upstream
revision and explains how to obtain it separately. Preserve its original licence
when using or redistributing that checkout. Our own frozen baseline implementations
and benchmark harness snapshots remain part of this project's source.
