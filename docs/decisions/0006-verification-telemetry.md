# ADR 0006: Instrument the Verification Workflow with Non-Sensitive Stage Telemetry

- **Status:** Accepted
- **Date:** 2026-08-26

## 1. Context

The prototype has an explicit stakeholder performance requirement:

> Routine label verification should complete in approximately five seconds.

Meeting that requirement requires measured evidence rather than assumptions.

A single UI stopwatch is not sufficient because it does not show where time is being spent.

Potential latency sources include:

- application-record lookup;
- image buffering;
- image validation;
- OCR;
- structured parsing;
- deterministic field verification;
- result aggregation;
- HTTP transport;
- browser rendering.

The application therefore needs telemetry at the verification-workflow boundary.

At the same time, label images and extracted document contents should not become routine application-log payloads merely for troubleshooting or performance measurement.

The telemetry design must therefore provide:

- useful performance evidence;
- workflow correlation;
- clear stage timing;
- failure diagnostics;
- privacy-conscious logging.

---

## 2. Decision

The Application-layer verification workflow will emit operational telemetry for each verification attempt.

The workflow-level telemetry model includes:

```text
CorrelationId
OcrDuration
VerificationDuration
TotalDuration
```

Operational logging also includes appropriate metadata such as:

```text
result category
error category
application identifier where appropriate
```

The telemetry intentionally excludes document contents.

---

## 3. Workflow Correlation

Each verification workflow receives its own correlation identifier.

Conceptually:

```text
VerificationCorrelationId
```

This identifier is distinct from the Web application's HTTP request correlation.

The two correlation concepts serve different purposes.

### HTTP Correlation

Used to trace:

```text
incoming HTTP request
ASP.NET Core request pipeline
HTTP response
```

### Verification Correlation

Used to trace:

```text
one label-verification workflow
```

Keeping these concepts separate allows the verification workflow to remain meaningful if it later runs through:

- batch processing;
- background workers;
- queue-triggered execution;
- CLI tooling;
- non-HTTP interfaces.

---

## 4. Timing Boundaries

The telemetry model defines explicit timing boundaries.

---

## 4.1 OCR Duration

`OcrDuration` measures the Application workflow's invocation of:

```text
ILabelTextExtractor
```

Conceptually:

```text
start OCR timer
    |
    v
ILabelTextExtractor.ExtractAsync(...)
    |
    v
stop OCR timer
```

This is the workflow-level OCR duration.

It is intentionally separate from any provider-specific timing that may also be returned by Azure Document Intelligence.

The Application layer owns the timing definition used for workflow analysis.

---

## 4.2 Verification Duration

`VerificationDuration` measures work performed after OCR evidence has been returned.

This includes the implemented deterministic verification path such as:

```text
structured parsing
brand comparison
ABV comparison
proof comparison
net-contents comparison
Government Warning verification
result aggregation
```

OCR is excluded from this timing.

This makes it possible to determine whether verification rules themselves are contributing materially to latency.

---

## 4.3 Total Duration

`TotalDuration` measures the complete Application-layer verification workflow.

This can include:

```text
application-record lookup
image buffering
image validation
OCR
structured parsing
verification
aggregation
result construction
```

The boundary begins when the Application verification workflow begins and ends when it returns its result.

---

## 5. What Total Duration Does Not Measure

Application-layer `TotalDuration` is not identical to full browser-observed response time.

It does not include all latency associated with:

```text
user browser
Internet transport to Azure App Service
browser rendering
Blazor UI rendering
client-side interaction time
```

For that reason, benchmark documentation must describe the timing boundary precisely.

The metric should not be presented as a browser end-to-end latency measurement.

---

## 6. Sensitive Data Policy

Verification telemetry must not include document contents.

Examples of information intentionally excluded include:

```text
uploaded image bytes
OCR document text
full extracted label text
parsed brand values
parsed addresses
Government Warning text
uploaded filenames
other extracted document contents
```

Operational logs should not become an alternate document-retention mechanism.

---

## 7. Allowed Operational Metadata

Useful non-document telemetry may include:

```text
verification correlation identifier
application identifier
result category
error category
OCR duration
verification duration
total duration
provider timeout category
```

The objective is to answer questions such as:

```text
How long did OCR take?
How long did deterministic verification take?
Did the workflow exceed the target?
Which stage failed?
Was the result PASS, REVIEW, or FAIL?
```

without logging the underlying label contents.

---

## 8. Result Semantics

Technical processing success and regulatory result category are separate concepts.

A technically successful workflow may produce:

```text
PASS
REVIEW
FAIL
```

For example:

```text
ProcessingSucceeded: true
ResultCategory: REVIEW
```

means the system successfully evaluated the available evidence but determined that human review is required.

It does not mean processing failed.

This distinction is important for:

- performance reporting;
- operational monitoring;
- benchmark interpretation;
- compliance workflow semantics.

---

## 9. Failure Semantics

A technical failure occurs when the workflow cannot complete normally.

Examples include:

```text
invalid image
application record not found
OCR timeout
OCR provider exception
unexpected technical failure
```

Technical failure information should be represented with an operational error category rather than converted into a regulatory `FAIL`.

For example:

```text
OCR_TIMEOUT
```

is a processing failure.

It is not evidence that the alcohol label itself is noncompliant.

---

## 10. Exception Timing

OCR timing is captured around the OCR workflow boundary even when the OCR provider throws.

Conceptually:

```text
start OCR stopwatch

try
{
    invoke OCR
}
finally
{
    stop OCR stopwatch
}
```

This preserves useful stage timing during failure diagnosis.

However, if an exception escapes before a normal verification result can be returned, the Application result object cannot carry normal completion telemetry to the caller.

The benchmark harness therefore requires an outer timing boundary for such attempts.

---

## 11. Benchmark Integration

The dedicated benchmark harness uses workflow telemetry when normal processing completes.

For a completed workflow:

```text
ObservedAttempt =
    Application-layer TotalDuration
```

If an exception occurs before workflow telemetry can be returned:

```text
ObservedAttempt =
    benchmark harness elapsed time
```

This prevents failed or timed-out attempts from being silently removed from the performance distribution.

---

## 12. Why Failed Attempts Must Remain in Performance Analysis

A performance benchmark can become misleading if it calculates statistics using only successful requests.

For example:

```text
45 requests complete in 2 seconds
5 requests time out after 5 seconds
```

If the five timeouts are excluded, the reported latency distribution would hide an important user-experience problem.

The benchmark therefore retains failed attempts as observed target misses.

This supports a more defensible performance claim.

---

## 13. Formal Benchmark Evidence

The implemented benchmark uses five representative synthetic labels.

Formal methodology:

```text
5 representative fixtures
1 excluded warm-up pass
10 measured iterations per fixture
50 measured observations
5-second OCR timeout
rotating fixture starting order
nearest-rank p95
```

Formal measured results:

| Metric | Result |
|---|---:|
| Measured observations | 50 |
| Successful workflows | 50 / 50 |
| Failed attempts | 0 |
| OCR timeouts during measured phase | 0 |
| Attempts meeting approximately five-second target | 50 / 50 |
| Median observed latency | 2.201 s |
| P95 observed latency | 2.620 s |
| Worst observed latency | 3.277 s |
| Median OCR latency | 2.201 s |
| P95 OCR latency | 2.619 s |
| Worst OCR latency | 3.276 s |
| Median deterministic verification latency | < 1 ms |

These measurements demonstrate that OCR dominates steady-state workflow latency.

---

## 14. First-Use Diagnostics

Separate diagnostics identified first-use/warm-up behavior.

Some early requests in fresh processes reached the configured five-second OCR timeout.

Immediate subsequent requests completed successfully.

The behavior moved with request position when fixture order was reversed.

The telemetry design helped isolate the fact that the delay occurred in the OCR path rather than in deterministic verification.

---

## 15. Privacy and Logging Rationale

The verification application may eventually operate on information subject to:

- privacy requirements;
- records-management requirements;
- security controls;
- data-retention policies;
- audit requirements.

Logging complete OCR payloads for routine operational troubleshooting would unnecessarily increase the quantity of sensitive or regulated information stored outside the primary business workflow.

The telemetry model therefore favors:

```text
metadata
durations
categories
correlation
```

over:

```text
document content
```

---

## 16. Observability Principle

The telemetry strategy follows this principle:

> Collect enough information to understand system behavior without collecting document contents that are unnecessary for that purpose.

This provides a stronger basis for future:

- application monitoring;
- performance dashboards;
- SLA/SLO tracking;
- failure analysis;
- capacity planning;
- operational alerting.

---

## 17. Separation from Provider Diagnostics

Azure Document Intelligence may expose its own diagnostic information.

That provider-owned information can be useful, but it should not define the Application-layer telemetry contract.

The architecture therefore distinguishes:

```text
Provider diagnostics
```

from:

```text
Application workflow telemetry
```

This preserves provider independence.

If the OCR implementation changes later, the Application telemetry model can remain stable.

---

## 18. Separation from UI Timing

The Web UI may display processing duration to the evaluator.

That display should use workflow telemetry rather than maintaining an independent UI stopwatch as the authoritative performance measurement.

This prevents inconsistencies between:

```text
UI duration
benchmark duration
service duration
```

and keeps timing semantics centralized in the Application workflow.

---

## 19. Correlation and Future Batch Processing

Workflow-level correlation is especially important for future batch processing.

A future batch may contain:

```text
200
300
or more labels
```

A batch-level identifier alone would be insufficient for troubleshooting individual labels.

A future design could use:

```text
BatchCorrelationId
        |
        +-- VerificationCorrelationId 1
        +-- VerificationCorrelationId 2
        +-- VerificationCorrelationId 3
        +-- VerificationCorrelationId N
```

The current workflow-level correlation design preserves that evolution path.

---

## 20. Consequences

### Positive

- Performance bottlenecks can be measured directly.
- OCR latency can be separated from deterministic verification latency.
- Performance claims are based on observed data.
- Workflow correlation is independent of HTTP.
- Sensitive document contents are excluded from routine telemetry.
- Benchmark results can retain timeout/failure attempts.
- UI timing can use the same authoritative workflow telemetry.
- Future batch processing has a natural per-item correlation model.
- Provider changes do not require redesigning the Application telemetry contract.

### Negative

- Multiple timing concepts must be documented carefully.
- Application `TotalDuration` is not full browser end-to-end latency.
- Exceptions that escape before result creation require an outer timing boundary.
- Additional telemetry code creates modest implementation complexity.
- Operational teams must understand the distinction between processing failure and compliance result.

---

## 21. Alternatives Considered

### 21.1 UI Stopwatch Only

Rejected.

A UI-only timer would:

- couple performance measurement to Blazor;
- make non-Web workflows harder to measure;
- provide no stage-level timing;
- make OCR bottlenecks harder to isolate.

---

### 21.2 Provider-Owned OCR Duration Only

Rejected.

Provider timing does not measure:

```text
application lookup
image validation
parsing
deterministic verification
aggregation
```

and would couple performance semantics to a particular provider.

---

### 21.3 Log the Complete OCR Payload

Rejected.

Full OCR contents are unnecessary for routine performance telemetry and would increase information-exposure and retention risk.

---

### 21.4 Log Uploaded Filenames

Rejected for routine workflow telemetry.

A filename may contain:

```text
applicant names
case identifiers
business names
internal tracking values
```

and is unnecessary for measuring workflow performance.

Correlation identifiers provide a safer troubleshooting mechanism.

---

### 21.5 Exclude Failed Attempts from Benchmarks

Rejected.

Removing timeouts or failures would bias the benchmark toward successful requests and could conceal poor user experience.

---

### 21.6 Treat REVIEW as a Technical Failure

Rejected.

`REVIEW` is a valid regulatory decision-support outcome.

It means:

```text
the workflow completed
but human judgment is required
```

not:

```text
the application failed
```

---

## 22. Production Evolution

A production implementation could extend the telemetry model with:

```text
distributed tracing
OpenTelemetry
Azure Monitor
Application Insights
centralized dashboards
SLO monitoring
alerting
batch-level metrics
provider error-rate metrics
OCR quality metrics
rule-version identifiers
deployment-version identifiers
```

Any expansion should preserve the current principle of avoiding unnecessary document-content logging.

Production telemetry would also need to comply with applicable:

- Treasury logging standards;
- records-management requirements;
- privacy requirements;
- retention policies;
- security controls.

---

## 23. Decision Summary

The verification workflow uses:

```text
VerificationCorrelationId
        |
        +--> OcrDuration
        |
        +--> VerificationDuration
        |
        +--> TotalDuration
        |
        +--> Result / Error Category
```

while intentionally excluding:

```text
OCR text
image bytes
extracted document contents
uploaded filenames
```

This provides enough evidence to understand system behavior, benchmark the five-second target, and troubleshoot failures without turning sensitive label content into routine operational telemetry.