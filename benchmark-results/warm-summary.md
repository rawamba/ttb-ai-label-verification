# Prototype Performance Benchmark

Generated UTC: `2026-08-27T02:05:30.3835615+00:00`

Git SHA: `96cdd6ca966a82f63ca72bc6c9b287ba2a574e6b`

Measured warm-state observations: **50**

Excluded warm-up observations: **5**

The stakeholder's approximately five-second target is evaluated against observed attempt latency. Completed workflows use the Application-layer `TotalDuration`; attempts that terminate before telemetry is returned use benchmark-harness elapsed time.

Timeouts and processing failures are retained as target misses rather than being removed from the performance distribution.

| Dataset | N | Success | OCR Timeouts | <=5s | Median Observed | P95 Observed | Worst Observed | Median OCR | P95 OCR | Median Verification |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Overall | 50 | 50 | 0 | 50/50 (100%) | 2,201 ms | 2,620 ms | 3,277 ms | 2,201 ms | 2,619 ms | 0 ms |
| brand-variation-label.png | 10 | 10 | 0 | 10/10 (100%) | 2,182 ms | 3,277 ms | 3,277 ms | 2,182 ms | 3,276 ms | 0 ms |
| compliant-label.png | 10 | 10 | 0 | 10/10 (100%) | 2,190 ms | 2,195 ms | 2,195 ms | 2,189 ms | 2,195 ms | 0 ms |
| compliant-with-glare.jpg | 10 | 10 | 0 | 10/10 (100%) | 2,203 ms | 2,253 ms | 2,253 ms | 2,202 ms | 2,253 ms | 0 ms |
| degraded-label.jpg | 10 | 10 | 0 | 10/10 (100%) | 2,206 ms | 2,482 ms | 2,482 ms | 2,206 ms | 2,482 ms | 0 ms |
| rotated-label.png | 10 | 10 | 0 | 10/10 (100%) | 2,302 ms | 2,620 ms | 2,620 ms | 2,302 ms | 2,619 ms | 0 ms |

## Benchmark Environment

- Benchmark location: Windows 11 developer workstation to Azure Document Intelligence
- Runtime: .NET 8.0.30
- .NET SDK: 10.0.300
- Operating system: Microsoft Windows 10.0.26200
- Process architecture: X64
- Logical processors visible to process: 16
- Azure region: East US 2
- Document Intelligence SKU: S0
- OCR endpoint host: docintel-ttb-label-verification-iwluomsqzvz26.cognitiveservices.azure.com
- OCR model: prebuilt-read
- OCR timeout: 5 seconds
- Font styling enabled: True
- Authentication: DefaultAzureCredential

## Methodology

- Five representative synthetic label fixtures were benchmarked.
- One full five-image warm-up pass was executed and excluded from formal statistics.
- Ten measured iterations produced 50 formal observations.
- Fixture starting position rotated each iteration to reduce ordering bias.
- The OCR timeout remained fixed at five seconds.
- P95 uses the nearest-rank percentile method.
- OCR and verification stage metrics are calculated only for completed verification workflows.
- Observed-attempt metrics retain timeout and failure latency.

## Timing Boundaries

- `ObservedAttempt`: complete benchmark-observed attempt latency and the primary five-second-target metric.
- `TotalDuration`: Application-layer workflow duration for completed workflows.
- `OcrDuration`: OCR abstraction latency measured at the Application workflow boundary.
- `VerificationDuration`: parsing, deterministic comparison, and aggregation after OCR.
- Browser rendering and Internet transport to the deployed Blazor UI are not included in these measurements.

No OCR text, image bytes, or extracted label field values are written to benchmark result files.
