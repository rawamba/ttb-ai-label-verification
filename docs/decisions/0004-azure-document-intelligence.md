# ADR 0004: Use Azure Document Intelligence Behind the OCR Abstraction

- **Status:** Accepted
- **Date:** 2026-08-26

## 1. Context

The prototype requires reliable text extraction from alcohol-label images that may contain real-world visual challenges such as:

- rotation or skew;
- glare;
- poor lighting;
- compression artifacts;
- low contrast;
- varied typography;
- dense regulatory text.

OCR is a critical dependency because all downstream parsing and verification depend on the quality of the evidence extracted from the label image.

The architecture also requires:

- an evaluator-accessible deployment;
- compatibility with the existing Azure environment;
- bounded external-service latency;
- OCR confidence information;
- line and word structure;
- supported font-style evidence for Government Warning evaluation;
- a path toward stronger production network isolation;
- provider isolation so deterministic verification rules are not coupled to an OCR vendor.

Earlier architecture planning considered local or containerized OCR as a possible way to minimize network latency.

The implemented prototype instead uses Azure Document Intelligence and validates its performance empirically.

---

## 2. Decision

The prototype will use:

```text
Azure Document Intelligence
```

as the primary OCR and document-perception provider.

The selected model is:

```text
prebuilt-read
```

Azure-specific OCR functionality is implemented in the Infrastructure layer behind the Application-layer abstraction:

```text
ILabelTextExtractor
        |
        v
DocumentIntelligenceLabelTextExtractor
        |
        v
Azure Document Intelligence
```

The verification workflow depends on:

```text
ILabelTextExtractor
```

rather than directly depending on Azure SDK types.

Provider-specific responses are converted into provider-neutral application evidence before parsing and compliance verification.

---

## 3. Architectural Boundary

The OCR provider is responsible for **perception**.

It may provide evidence such as:

```text
detected text
lines
words
confidence information
supported style information
```

The OCR provider does **not** determine regulatory compliance.

The boundary is intentionally:

```text
Label Image
    |
    v
Azure Document Intelligence
    |
    | perception / evidence extraction
    v
Provider-Neutral OCR Evidence
    |
    v
Structured Parser
    |
    v
Deterministic Verification Rules
    |
    v
PASS / REVIEW / FAIL
    |
    v
Human Compliance Agent
```

This preserves a clear distinction between probabilistic evidence extraction and deterministic regulatory comparison.

---

## 4. Configuration

The current prototype uses:

```text
Model:
prebuilt-read

OCR timeout:
5 seconds

Font-style extraction:
enabled
```

The five-second timeout is intentional.

It reflects the stakeholder requirement that routine label verification should complete in approximately five seconds rather than reproducing the 30–40 second latency experienced in an earlier scanning approach.

The timeout is not increased merely to hide slow-provider behavior.

---

## 5. Provider-Neutral Application Contract

The Application layer owns the OCR abstraction.

Conceptually:

```csharp
public interface ILabelTextExtractor
{
    Task<OcrResult> ExtractAsync(
        Stream image,
        CancellationToken cancellationToken);
}
```

The exact contract may evolve, but the architectural rule remains:

> Application code should consume application-owned OCR evidence rather than Azure SDK response objects.

This allows future providers to implement the same abstraction.

Possible future implementations could include:

```text
PaddleOCR
another Azure model
a custom trained document model
a multimodal fallback
another approved OCR provider
```

without rewriting deterministic verification rules.

---

## 6. Why Azure Document Intelligence

Azure Document Intelligence was selected because it provides the capabilities needed by the prototype while fitting the current Azure deployment model.

Relevant capabilities include:

- managed OCR;
- printed-text extraction;
- line-level evidence;
- word-level evidence;
- confidence information;
- supported style information;
- .NET SDK support;
- Azure Identity integration;
- Managed Identity support;
- Azure RBAC support;
- compatibility with Azure App Service;
- future private-networking options.

This provides a practical balance between implementation speed, evaluator accessibility, security, and architectural separation.

---

## 7. Performance Evidence

The OCR provider was evaluated using the dedicated prototype benchmark harness.

The formal warm-state benchmark consisted of:

```text
5 representative fixtures
×
10 measured iterations
=
50 measured observations
```

A complete five-image warm-up pass was performed separately and excluded from formal latency statistics.

### Measured Results

| Metric | Result |
|---|---:|
| Measured observations | 50 |
| Successful workflows | 50 / 50 |
| OCR timeouts during measured phase | 0 |
| Attempts meeting approximately five-second target | 50 / 50 |
| Median observed latency | 2.201 s |
| P95 observed latency | 2.620 s |
| Worst observed latency | 3.277 s |
| Median OCR latency | 2.201 s |
| P95 OCR latency | 2.619 s |
| Worst OCR latency | 3.276 s |
| Median deterministic verification latency | < 1 ms |

The results demonstrate that OCR accounts for nearly all measured steady-state workflow latency.

The deterministic verification path is not the significant latency contributor.

---

## 8. First-Use / Warm-Up Behavior

Separate diagnostic testing identified a first-use effect in the end-to-end OCR path.

### Original Fixture Order

During a fresh-process first pass:

```text
2 early OCR requests reached the 5-second timeout
3 requests completed
```

An immediate second pass completed:

```text
5 / 5 successfully
```

### Reversed Fixture Order

During another fresh-process test with reversed fixture order:

```text
3 early OCR requests reached the 5-second timeout
2 requests completed
```

The immediate second pass again completed:

```text
5 / 5 successfully
```

The timeout behavior moved with request position rather than remaining attached to specific images.

This supports the conclusion that the observed behavior is a **first-use / warm-up effect** rather than a consistent image-specific OCR defect.

---

## 9. Warm-Up Attribution

The prototype does **not** attribute the first-use effect solely to Azure Document Intelligence.

Possible contributors include:

```text
Azure credential discovery
token acquisition
.NET runtime initialization
JIT compilation
Azure SDK initialization
TLS establishment
HTTP connection establishment
DNS resolution
network variability
provider-side processing variability
```

The benchmark therefore distinguishes:

```text
first-use diagnostic behavior
```

from:

```text
formal warm-state benchmark behavior
```

This prevents the documentation from making an unsupported claim about the source of latency.

---

## 10. CI Boundary

Normal continuous integration must remain deterministic.

Therefore:

```text
RUN_LIVE_OCR_TESTS=false
```

is used in standard CI.

Live OCR behavior is validated separately through:

- opt-in live integration testing; and
- the dedicated benchmark harness.

This prevents transient Azure availability or provider latency from determining whether deterministic verification logic passes CI.

---

## 11. Security Boundary

The browser does not call Azure Document Intelligence directly.

The request path is:

```text
Browser
    |
    | HTTPS
    v
Azure App Service
    |
    | server-side authenticated request
    v
Azure Document Intelligence
```

OCR credentials are therefore not exposed to browser clients.

Authentication to Azure Document Intelligence is handled separately through Managed Identity and Azure RBAC.

See:

```text
docs/decisions/0005-managed-identity-and-rbac.md
```

---

## 12. Network Boundary

The evaluator-accessible prototype currently uses the public Azure Document Intelligence service endpoint.

This is an intentional prototype decision.

It allows the deployed application to remain externally evaluable without depending on Treasury internal networking.

For production, the architecture supports evolution toward:

```text
Azure App Service
    |
    v
VNet Integration
    |
    v
Private DNS
    |
    v
Private Endpoint
    |
    v
Azure Document Intelligence
```

A production deployment could additionally disable public AI access and apply firewall and NSG restrictions.

No deterministic verification logic would need to change.

---

## 13. Consequences

### Positive

- The prototype exercises a real managed OCR provider.
- OCR remains isolated behind an Application-layer interface.
- Azure-specific types do not propagate through deterministic verification logic.
- Confidence and structural evidence are available.
- Supported style information can assist Government Warning evaluation.
- Managed Identity can be used in Azure.
- Azure App Service deployment remains straightforward.
- Performance has been measured rather than assumed.
- The architecture retains a path to alternative OCR providers.
- Normal CI remains independent of the live provider.

### Negative

- OCR requires an external network call.
- External-service latency is inherently variable.
- First-use requests may exceed the five-second target.
- Live OCR depends on Azure identity and connectivity.
- Production private networking requires additional infrastructure.
- Provider behavior may evolve independently of application code.
- OCR evidence quality remains dependent on image quality.

---

## 14. Alternatives Considered

### 14.1 Local OCR as the Primary Provider

A local OCR implementation was considered because it could potentially eliminate a remote service round trip.

It was not selected for the implemented MVP because:

- Azure Document Intelligence already satisfied the required OCR capability;
- introducing a second OCR runtime would increase prototype operational complexity;
- containerized or native OCR dependencies could complicate deployment;
- Azure OCR could be benchmarked directly against the five-second requirement;
- the abstraction already preserves the ability to introduce local OCR later.

A local provider remains a possible future implementation of:

```text
ILabelTextExtractor
```

---

### 14.2 Direct Azure SDK Usage in the Web Layer

Rejected.

That approach would couple:

```text
Blazor UI
Azure SDK
OCR behavior
verification workflow
```

and make provider replacement and deterministic testing more difficult.

The OCR provider belongs in Infrastructure.

---

### 14.3 Direct Azure SDK Usage Throughout Application Logic

Rejected.

Provider-specific types should not become the contract for regulatory verification.

The Application layer owns the evidence model.

---

### 14.4 Generative AI for Every Verification

Rejected for the normal verification path.

Objective comparisons such as:

```text
45% ABV vs. 40% ABV
750 mL vs. 1 L
expected proof vs. detected proof
```

do not require unrestricted generative reasoning.

Deterministic comparison is more:

- explainable;
- reproducible;
- testable;
- predictable;
- performant.

---

### 14.5 Increase the OCR Timeout

Not selected as a response to the observed first-use latency.

Increasing the timeout would make benchmark results appear more favorable while weakening the stakeholder's approximately five-second requirement.

The current approach instead:

```text
retains the five-second timeout
documents first-use behavior
measures steady-state performance separately
```

---

## 15. Future Evolution

The OCR abstraction supports future enhancements without changing the core verification architecture.

Potential future options include:

### Local OCR Provider

A provider such as PaddleOCR could be introduced for environments where local processing is operationally preferable.

### Query-Field Extraction

Azure Document Intelligence query-field capabilities could be evaluated if they improve extraction reliability for known regulatory fields.

### Custom Document Model

A custom model could be considered if sufficient representative training data and measurable quality gains justify the added lifecycle complexity.

### Multimodal Fallback

A multimodal model could be used selectively for difficult or ambiguous labels.

Any such fallback should remain outside deterministic regulatory authority.

### Image Preprocessing

Additional preprocessing could include:

```text
deskewing
rotation correction
contrast adjustment
glare mitigation
cropping
perspective correction
```

if benchmarks show measurable benefit.

---

## 16. Decision Summary

The prototype uses:

```text
Azure Document Intelligence
        |
        v
prebuilt-read
        |
        v
ILabelTextExtractor abstraction
        |
        v
Provider-neutral evidence
        |
        v
Deterministic verification
        |
        v
Human review
```

This decision provides a production-relevant OCR capability while preserving provider isolation, measurable performance, deterministic compliance logic, and future technology flexibility.