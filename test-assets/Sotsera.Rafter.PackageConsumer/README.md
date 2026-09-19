# Package consumer fixture

This project is intentionally excluded from `Rafter.slnx`. It consumes the packed package, so restoring it before
the package exists would make ordinary solution restore order-dependent. `eng/verify-package.ps1` copies both the
conventional project and `consumer.cs` file-based app to a temporary directory, points NuGet at the local package
output, restores them with an isolated package cache, and runs them. Both consumers bind an option and execute a
target through the public API. The temporary path contains spaces, and the repository's SDK selection is copied
alongside the consumers. The verifier separately checks the packaged PDB identity and Source Link mapping.
