<!--
TTB AI-Powered Alcohol Label Verification
Contributor Guide

Purpose:
- Make the prototype easy for another engineer or evaluator to understand,
  reproduce, modify, and review.
- Preserve the architectural boundaries demonstrated by the take-home project.
-->

# Contributing

Thank you for contributing to the **AI-Powered Alcohol Label Verification** prototype.

This repository demonstrates an engineering approach for accelerating alcohol-label review while preserving explainability, deterministic compliance logic, technical fault isolation, and human decision authority.

Contributions should strengthen those characteristics rather than obscure them.

---

## Engineering Principles

Changes should preserve the following project boundaries:

```text
AI / OCR
    ↓
Perception evidence
    ↓
Structured interpretation
    ↓
Deterministic verification
    ↓
PASS / REVIEW / FAIL
    ↓
Human compliance judgment
```

Technical processing failures remain separate:

```text
Technical failure
    ↓
ERROR
```

An `ERROR` must not be converted into a regulatory `FAIL`.

The prototype also intentionally keeps production COLA integration behind an adapter boundary.

Do not invent or imply a production COLA API contract that is not available to the project.

---

## Development Workflow

The repository uses a feature-branch and pull-request workflow.

Start from an up-to-date `main` branch:

```powershell
# Synchronize the local main branch before beginning new work.
git checkout main
git pull origin main

# Create a focused feature branch.
git checkout -b feature/descriptive-change-name
```

Keep each branch focused on one coherent engineering change.

Examples:

```text
feature/batch-label-verification
feature/image-preprocessing
docs/evaluator-walkthrough
fix/ocr-timeout-handling
```

Avoid combining unrelated refactoring, infrastructure changes, UI changes, and feature work unless they are required by the same implementation slice.

---

## Solution Structure

```text
src/
  LabelVerification.Domain/
  LabelVerification.Application/
  LabelVerification.Infrastructure/
  LabelVerification.Web/

tests/
  LabelVerification.UnitTests/
  LabelVerification.IntegrationTests/

tools/
  LabelVerification.Benchmarks/

sample-data/
docs/
infra/
```

Responsibilities are intentionally separated:

| Project | Responsibility |
|---|---|
| `LabelVerification.Domain` | Provider-independent business concepts |
| `LabelVerification.Application` | Workflow orchestration and deterministic verification behavior |
| `LabelVerification.Infrastructure` | Azure OCR and external-provider implementations |
| `LabelVerification.Web` | Blazor Server presentation and application composition |
| `LabelVerification.UnitTests` | Deterministic behavioral testing |
| `LabelVerification.IntegrationTests` | Composition and workflow integration testing |
| `LabelVerification.Benchmarks` | Explicit live performance measurement |

Regulatory rules should not be implemented directly inside Razor components.

Azure-specific SDK types should remain inside Infrastructure where practical.

---

## Local Prerequisites

The projects target .NET 8.

Development validation has been performed with:

```text
.NET SDK 10.0.300
.NET runtime 8.x
PowerShell
Azure CLI
```

Live OCR work additionally requires an Azure identity authorized to invoke the configured Azure Document Intelligence resource.

---

## Build

From the repository root:

```powershell
# Restore dependencies.
dotnet restore LabelVerification.slnx

# Compile the complete solution.
dotnet build LabelVerification.slnx `
    --configuration Release
```

The repository should build without warnings introduced by the proposed change.

---

## Deterministic Tests

Normal validation does not require the live Azure OCR service.

```powershell
# Ensure the opt-in live OCR test is disabled.
Remove-Item Env:RUN_LIVE_OCR_TESTS `
    -ErrorAction SilentlyContinue

# Run the complete deterministic test baseline.
dotnet test LabelVerification.slnx `
    --configuration Release
```

When adding or changing behavior, update tests that demonstrate:

- the expected successful path;
- important boundary conditions;
- error behavior;
- cancellation where relevant;
- PASS / REVIEW / FAIL behavior where relevant; and
- separation of technical `ERROR` from regulatory outcomes.

Do not make deterministic CI depend on transient external-service availability.

---

## Live OCR Testing

Azure Document Intelligence integration testing is intentionally opt-in.

Configure the required environment first.

Example:

```powershell
# Enable the live OCR integration test deliberately.
$env:RUN_LIVE_OCR_TESTS =
    "true"

# Configure the approved Azure Document Intelligence endpoint.
$env:DocumentIntelligence__Endpoint =
    "https://docintel-ttb-label-verification-iwluomsqzvz26.cognitiveservices.azure.com/"

# Use the prototype OCR model.
$env:DocumentIntelligence__ModelId =
    "prebuilt-read"

# Bound credential/token startup separately.
$env:DocumentIntelligence__AuthenticationTimeoutSeconds =
    "15"

# Preserve the latency-sensitive OCR provider-operation timeout.
$env:DocumentIntelligence__TimeoutSeconds =
    "5"
```

Then run:

```powershell
# Run the integration suite with live OCR explicitly enabled.
dotnet test `
    ".\tests\LabelVerification.IntegrationTests\LabelVerification.IntegrationTests.csproj" `
    --configuration Release
```

Do not commit personal credentials or access tokens.

---

## Performance Changes

Performance claims should be measured rather than inferred.

The repository includes a dedicated benchmark harness under:

```text
tools/LabelVerification.Benchmarks/
```

For changes expected to affect OCR or batch throughput, capture:

- benchmark environment;
- model configuration;
- timeout configuration;
- sample size;
- concurrency;
- warm-up behavior;
- median;
- p95;
- worst observed latency;
- technical error count; and
- relevant throughput.

Keep **per-label latency** separate from **whole-batch wall-clock time**.

A 300-label batch is not expected to complete in five seconds merely because the routine per-label target is approximately five seconds.

---

## Test Data

Use synthetic data for repository fixtures.

Representative fixtures are located under:

```text
sample-data/labels/verification/
```

Do not add production applicant data or sensitive production label images.

When adding a fixture, document:

- what behavior it exercises;
- the intended result;
- whether it represents a regulatory mismatch, ambiguity, image-quality condition, or technical condition; and
- any known limitation.

---

## Security Expectations

Do not commit:

```text
passwords
API keys
access tokens
private certificates
secret connection strings
production applicant records
non-public COLA data
sensitive production documents
```

Azure-hosted service access should prefer:

```text
Managed Identity
+
Scoped Azure RBAC
```

See [`SECURITY.md`](SECURITY.md) for vulnerability-reporting guidance.

---

## Documentation Expectations

Code and configuration changes should include comments where they clarify:

- intent;
- architectural boundaries;
- non-obvious behavior;
- timeout semantics;
- security decisions;
- regulatory limitations; or
- reasons for an implementation trade-off.

Avoid comments that merely repeat the syntax of the next line.

Evaluator-facing behavior should remain consistent across:

```text
README.md
docs/architecture.md
relevant ADRs
application behavior
tests
benchmark evidence
```

Do not describe planned functionality as already implemented.

---

## Pull Requests

A pull request should be small enough for another engineer to review confidently.

Before opening a PR:

```powershell
# Verify repository state.
git status

# Compile the final working tree.
dotnet build LabelVerification.slnx `
    --configuration Release

# Run deterministic tests.
dotnet test LabelVerification.slnx `
    --configuration Release `
    --no-build

# Check staged content for whitespace problems.
git diff --cached --check
```

The pull request should explain:

- what changed;
- why it changed;
- important architecture or security implications;
- how it was tested;
- any relevant performance evidence; and
- known limitations or deliberate exclusions.

---

## Commit Messages

Use concise, descriptive commit messages.

Examples:

```text
feat: add bounded batch label verification
fix: isolate OCR startup readiness from provider timeout
test: add batch workflow integration coverage
docs: clarify regulatory automation boundary
```

When the change is associated with an Azure Boards work item, include the applicable `AB#` reference.

---

## Regulatory Changes

Changes to deterministic regulatory behavior deserve additional scrutiny.

A regulatory-rule change should identify:

- the affected field or requirement;
- the source or rationale for the rule;
- whether the behavior is deterministic or judgment-based;
- expected PASS / REVIEW / FAIL behavior;
- regression tests; and
- any unsupported boundary.

If the evidence does not support a confident automated decision, prefer `REVIEW`.

---

## Definition of Done

A change is ready for review when:

```text
Build succeeds
+
Deterministic tests pass
+
Relevant new behavior is tested
+
Security boundaries are preserved
+
Documentation matches implementation
+
Known limitations remain explicit
+
No secrets or sensitive production data are introduced
```

---

## Final Principle

The project favors:

```text
Small changes.

Visible assumptions.

Reproducible evidence.

Explainable behavior.

Human judgment where judgment is required.
```