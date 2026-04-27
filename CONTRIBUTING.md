# Contributing to nahook-dotnet

Thanks for considering a contribution! A few important things to know first.

## Source of truth

This repository is a **subtree-split mirror** of the .NET SDK from our private monorepo `getnahook/nahook`. PRs filed directly here **cannot be merged** — the next subtree-push from the monorepo will force-overwrite this branch.

## What we welcome

- **Bug reports** — open a GitHub issue with: reproduction steps, SDK version, .NET version (`dotnet --version`), runtime (`net6.0` / `net8.0`), OS.
- **Feature requests** — open an issue describing the use case and the API surface you'd want.
- **Small code suggestions** — paste a snippet in an issue and describe intent; we'll port it into the monorepo and credit you in the resulting commit.
- **Substantial patches** — email `support@nahook.com` first; we'll hand-port your change into the monorepo and credit you in the resulting commit.

## Local development

```bash
git clone https://github.com/getnahook/nahook-dotnet
cd nahook-dotnet
dotnet build -c Release
dotnet test -c Release       # unit tests pass; integration tests skip without infra
```

The csproj declares `<TargetFrameworks>net6.0;net8.0</TargetFrameworks>`. SDK supports .NET 6+ (LTS) and .NET 8+.

### Code style

- `dotnet format whitespace --verify-no-changes` must be clean (CI enforces)
- `dotnet format style --verify-no-changes` must be clean (CI enforces)
- Brace style for switch case-blocks is pinned via `.editorconfig` (Allman, aligned with `case`)
- xUnit for tests

## License

By contributing, you agree your changes are released under the [MIT License](LICENSE).
