# Nuke CI/CD MCP Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Nuke-owned, type-safe CI/CD setup for RawSqlMcp with schema-correct MCP metadata, NuGet/MCP Registry release automation, local VHS docs assets, formatting hooks, README, and license packaging.

**Architecture:** A standard Nuke build project owns restore/build/test/pack/coverage/release/hook/VHS targets and generates GitHub Actions workflows from C# attributes. Project metadata remains in `RawSqlMcp.Cli.csproj` and `.mcp/server.json`, with Nuke validating the source and packed metadata. Git hooks stay thin and invoke one Nuke pre-commit target.

**Tech Stack:** .NET 10, Nuke 10, MinVer, Microsoft Testing Platform coverage, NuGet Trusted Publishing, MCP publisher CLI, GitHub Actions, Dependabot, VHS.

---

### Task 1: MCP Metadata Validation

**Files:**
- Create: `tests/RawSqlMcp.Cli.Tests.Unit/Packaging/ServerJsonTests.cs`
- Modify: `src/RawSqlMcp.Cli/.mcp/server.json`
- Modify: `src/RawSqlMcp.Cli/RawSqlMcp.Cli.csproj`

- [ ] Add tests that parse `server.json` and assert current schema-required fields, SQL Server wording, version placeholders, NuGet package metadata, and schema-correct environment variable casing.
- [ ] Run the new test and verify it fails against the current metadata.
- [ ] Update `server.json` to include `$schema`, `title`, top-level `version`, SQL Server description, package version placeholder, and `environmentVariables`.
- [ ] Update the MSBuild target to replace every `0.0.0-placeholder` occurrence before packing.
- [ ] Re-run unit tests and verify they pass.

### Task 2: Standard Nuke Build

**Files:**
- Create/modify: `.nuke/*`
- Create/modify: `build/*`
- Modify: `RawSqlMcp.slnx`
- Modify: `.gitignore`

- [ ] Scaffold a standard Nuke build project.
- [ ] Add targets for restore, format, build, unit tests, coverage, pack, source `server.json` validation, packed `server.json` inspection, local full validation, release dry run, publish release, VHS generation, hook installation, and pre-commit.
- [ ] Keep all substantive automation in C# targets.
- [ ] Make the pre-commit target restore, format, build, run current unit tests by project path, generate VHS GIFs, and stage only files changed by formatting or GIF generation.
- [ ] Verify Nuke build project compiles.

### Task 3: Generated GitHub Actions and Dependabot

**Files:**
- Generate: `.github/workflows/*.yml`
- Create: `.github/dependabot.yml`

- [ ] Add Nuke `GitHubActions` attributes for CI on `master` push/PR with README/docs path ignore and workflow dispatch.
- [ ] Add Nuke release workflow attributes for tag-based release from `master`, using Ubuntu latest and required permissions/secrets.
- [ ] Generate workflow YAML from the Nuke attributes and commit the generated source.
- [ ] Add monthly grouped Dependabot updates for NuGet and GitHub Actions.

### Task 4: README, License, and VHS Assets

**Files:**
- Modify: `README.md`
- Create: `LICENSE`
- Create: `docs/vhs/install-run.tape`
- Create: `docs/vhs/end-to-end-placeholder.tape`
- Create: `docs/vhs/install-run.gif` when VHS is available locally
- Modify: `src/RawSqlMcp.Cli/RawSqlMcp.Cli.csproj`

- [ ] Add AGPL-3.0-or-later license text and include it in the NuGet package via `PackageLicenseFile`.
- [ ] Write README focused on dnx installation, SQL Server configuration, and usage.
- [ ] Add a reproducible install/run tape that uses local build output while README documents `dnx RawSqlMcp`.
- [ ] Add a placeholder end-to-end tape for future multi-engine/demo support.
- [ ] Generate GIF locally if VHS is installed; otherwise keep the tape and make Nuke fail clearly when GIF generation is requested without VHS.

### Task 5: Verification

**Files:**
- All touched files.

- [ ] Run `dotnet test RawSqlMcp.slnx`.
- [ ] Run Nuke full validation.
- [ ] Run Nuke pack inspection.
- [ ] Run Nuke workflow generation and inspect generated workflow triggers.
- [ ] Run Nuke pre-commit target if VHS is installed; otherwise verify it fails with a clear VHS dependency message.
- [ ] Review `git diff` for unrelated churn.

