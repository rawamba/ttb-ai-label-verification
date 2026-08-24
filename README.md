# AI-Powered Alcohol Label Verification App

## Overview

This prototype is a human-in-the-loop decision-support tool designed to reduce the amount of routine visual comparison performed by alcohol label compliance agents.

The application extracts information from submitted label artwork, compares application-derived fields using field-specific verification rules, evaluates selected regulatory requirements, and presents an explainable result as:

- **PASS**
- **REVIEW**
- **FAIL**

The design intentionally separates deterministic compliance logic from AI-assisted extraction and ambiguity resolution. Routine and high-confidence checks are handled with explicit rules wherever possible, while uncertain cases are surfaced for human review rather than automatically adjudicated.

> **Design principle:** Apply AI to perception and ambiguity, deterministic rules to objective compliance checks, and human judgment to final compliance decisions.

The prototype is designed around the stakeholder's approximately five-second usability target. The common verification path favors local OCR and deterministic comparison logic to minimize avoidable network and model latency.

---

## Technology Stack

- **Application Platform:** .NET 8
- **User Interface:** Blazor Server
- **Language:** C#
- **OCR:** Local containerized OCR service
- **Verification:** Domain-level deterministic, normalized, and fuzzy-matching rules
- **Application Data:** JSON fixtures accessed through an adapter abstraction
- **Container Runtime:** Docker / Docker Compose
- **Testing:** .NET unit and integration tests
- **Architecture:** Layered application, domain, infrastructure, and presentation components

The technology choices intentionally favor compatibility with the stakeholder's existing .NET environment, local execution for the normal verification path, and clean integration boundaries for future COLA or Azure-based services.

---

## Stakeholder Requirements Addressed

| Stakeholder Need | Prototype Design Response |
|---|---|
| Results should return in approximately 5 seconds | Local OCR, deterministic verification, and minimal dependency on remote inference |
| Simple user experience | Single-label workflow with clear PASS / REVIEW / FAIL results |
| Brand-name variations require judgment | Normalization and fuzzy comparison with confidence thresholds |
| Government warning must be exact | Separate deterministic warning validator |
| Agents have varying technical comfort | Minimal controls and clear results; no AI knowledge required to operate the application |
| Poor-quality label images occur | Low-confidence extraction can trigger REVIEW rather than an unsupported rejection |
| Large submission batches occur | Verification engine is designed to support future queued batch processing |
| Production environment may restrict outbound traffic | Core OCR and deterministic verification do not require unrestricted Internet access |
| Human expertise remains important | Ambiguous cases are explicitly routed to compliance-agent review |

---

## Architecture

The solution follows a layered design that isolates user interaction, application orchestration, domain verification rules, and infrastructure dependencies.

- `LabelVerification.Domain` contains comparison and supported compliance rules and does not depend on a specific OCR engine or COLA implementation.
- `LabelVerification.Application` orchestrates extraction and verification use cases.
- `LabelVerification.Infrastructure` provides OCR and application-data adapter implementations.
- `LabelVerification.Web` provides the compliance-agent user interface.

This separation allows OCR engines, application-data sources, and a future authorized COLA integration to evolve without requiring changes to the core verification rules.

### High-Level Architecture

```text
Compliance Agent
       |
       v
Blazor Server UI
       |
       v
Application / Orchestration Layer
       |
       +----------------------+
       |                      |
       v                      v
Application Adapter      OCR / Extraction
       |                      |
       v                      v
Expected Values        Extracted Label Data
       |                      |
       +----------+-----------+
                  |
                  v
          Verification Engine
                  |
          +-------+-------+
          |               |
          v               v
 Application Matching   Regulatory Rules
          |               |
          +-------+-------+
                  |
                  v
       Confidence / Evidence
                  |
                  v
          PASS / REVIEW / FAIL
                  |
                  v
           Compliance Agent
```

---

## Scope

### Implemented Prototype Scope

The prototype focuses on verification of common label fields identified in the assignment:

- Brand name
- Class / type
- Alcohol by volume
- Proof, where applicable
- Net contents
- Government warning presence and supported text validation

The prototype does not integrate directly with the production COLA system.

### Out of Scope

The following are intentionally outside the scope of this proof of concept:

- Direct production COLA integration
- Federal authentication / SSO
- Production document retention
- Full beverage-specific regulatory coverage
- Automated final compliance adjudication
- Production ATO / FedRAMP implementation
- Long-term storage of submitted label images

These boundaries keep the exercise focused on label extraction, verification logic, usability, performance, and architectural design.

---

## Assumptions and System Boundary

The stakeholder notes establish that compliance agents compare information displayed on a label against corresponding application data.

The assignment does not provide:

- a COLA API contract,
- a database schema,
- a sample application payload, or
- authorization details for COLA access.

Because the take-home explicitly permits reasonable assumptions, the prototype treats the application record as an upstream dependency and represents it through a small structured application contract.

For demonstration and testing, sample application records are supplied as JSON fixtures based on fields identified in the assignment.

This creates a deliberate integration boundary:

```
Current Prototype

JSON Fixture
     |
     v
Application Adapter
     |
     v
Verification Engine

Future Integration

COLA / Authorized API
     |
     v
Application Adapter
     |
     v
Verification Engine
```

The verification engine therefore does not depend on the structure or implementation details of the current COLA system.

---

## Prototype Data Flow

```
                    Application Record
                    (Mock JSON Fixture)
                           |
                           v
                  Structured Expected Data
                           |
                           |
Label Image                |
    |                      |
    v                      |
Image Validation           |
    |                      |
    v                      |
OCR / Text Extraction      |
    |                      |
    v                      v
Extracted Label Data ---> Verification Engine
                               |
                    +----------+----------+
                    |                     |
                    v                     v
            Application Match      Regulatory Rules
            - Brand Name           - Government Warning
            - Class / Type         - Required Elements
            - ABV                  - Supported Formatting
            - Proof
            - Net Contents
                    |
                    v
            Confidence / Evidence
                    |
                    v
             PASS / REVIEW / FAIL
                    |
                    v
              Compliance Agent
```

---

## Example Application Contract

```
{
  "applicationId": "COLA-84729",
  "beverageType": "distilled_spirits",
  "expectedData": {
    "brandName": "Old Tom Distillery",
    "classType": "Kentucky Straight Bourbon Whiskey",
    "alcoholByVolume": 45.0,
    "proof": 90,
    "netContents": {
      "value": 750,
      "unit": "mL"
    }
  }
}
```

The government warning is intentionally modeled as a regulatory rule rather than application data because it represents a compliance requirement rather than a value unique to an individual COLA application.

---

## Verification Philosophy

The application uses different comparison strategies for different types of information.

### Application-Derived Fields

| Field | Verification Strategy |
|---|---|
| Brand Name | Normalization + fuzzy comparison |
| Class / Type | Normalized comparison, with ambiguity surfaced for review |
| Alcohol by Volume | Deterministic numeric comparison |
| Proof | Deterministic numeric comparison |
| Net Contents | Normalized value/unit comparison |

For example:

```
Application:
Stone's Throw

Detected:
STONE'S THROW

Normalized:
stones throw
stones throw

Result:
MATCH
```

Capitalization or punctuation differences therefore do not automatically create a false mismatch when the underlying brand value is clearly equivalent.

### Regulatory Checks

| Requirement | Verification Strategy |
|---|---|
| Government Warning | Strict rule-based text validation |
| Required Elements | Deterministic presence checks |
| Supported Formatting Requirements | Rule-based validation where extraction technology provides sufficient evidence |

The system does not use a generative model as the final authority for deterministic compliance decisions.

AI-assisted reasoning may be introduced for uncertain extraction or classification cases, but ambiguous evidence is routed to human review rather than being treated as a definitive compliance decision.

---

## Human-in-the-Loop Decision Model

The system intentionally avoids a simple automated approved/rejected decision.

- **PASS:** Used when required fields are detected with sufficient confidence and applicable comparisons pass deterministic validation.
- **REVIEW:** Used when:
  - OCR confidence is low;
  - the image is difficult to read;
  - fuzzy comparison falls within an ambiguous range;
  - a field cannot be reliably classified; or
  - automated evidence is insufficient for a deterministic result.
- **FAIL:** Used when the system has sufficient evidence of a clear application mismatch or a supported regulatory-rule failure.

Final compliance authority remains with the human compliance agent.

The automated result therefore represents **decision-support evidence**, not autonomous regulatory adjudication.

---

## Performance Strategy

A key stakeholder requirement is usability within approximately five seconds for routine label checks.

The prototype therefore keeps the normal processing path lightweight:

```
Image Validation
      |
      v
Local OCR
      |
      v
Field Parsing
      |
      v
Deterministic Verification
      |
      v
Result Rendering
```

Remote or expensive AI inference is not required for the normal high-confidence path.

Latency should be measured at major pipeline stages so performance bottlenecks can be identified empirically rather than inferred.

### Measured Prototype Performance

> **Replace this section with final benchmark results before submission.**

| Stage | Median | Worst / P95 |
|---|---:|---:|
| Image preprocessing | `<MEASURED>` | `<MEASURED>` |
| OCR | `<MEASURED>` | `<MEASURED>` |
| Field extraction | `<MEASURED>` | `<MEASURED>` |
| Verification | `<MEASURED>` | `<MEASURED>` |
| Result rendering | `<MEASURED>` | `<MEASURED>` |
| **Total** | **`<MEASURED>`** | **`<MEASURED>`** |

**Benchmark environment:** `<CPU / RAM / OS / SAMPLE SIZE>`

Performance varies based on image dimensions, image quality, rotation, and OCR complexity.

The primary acceptance objective is that routine labels complete within the stakeholder's approximately five-second usability target.

---

## Batch Processing

Stakeholder interviews identified a need to handle large submissions containing approximately 200–300 labels.

The initial proof of concept prioritizes a correct and responsive single-label verification workflow.

Because the verification engine is stateless with respect to an individual label operation, batch processing can be added without changing the underlying comparison rules.

A future prototype extension could use bounded concurrency:

```
Batch Upload
     |
     v
Work Queue
     |
     +------> Worker
     |
     +------> Worker
     |
     +------> Worker
     |
     v
Aggregated Results
```

For production-scale processing, the in-memory queue could be replaced by durable messaging and secure temporary object storage, allowing horizontal worker scaling and failure recovery.

---

## Security and Privacy

The take-home prototype does not require storage of sensitive production records; however, the design preserves clear security boundaries.

### Prototype Security Principles

- No API keys exposed to browser clients
- Uploaded file-type and size validation
- Controlled temporary processing
- No requirement for persistent storage of label artwork
- Structured error handling
- No unrestricted model authority over compliance decisions
- No logging of sensitive document contents

### Production Considerations

A production Treasury deployment would additionally require consideration of:

- Microsoft Entra ID / federal identity integration
- Managed identities
- Least-privilege RBAC
- Private endpoints
- Network allow-listing
- Encryption in transit and at rest
- Audit logging
- Records-management requirements
- Document-retention policies
- PII handling
- Security assessment and authorization
- Appropriate NIST and Treasury security controls

---

## Network-Constrained Deployment

Stakeholder discovery identified restricted outbound connectivity as an important operational constraint.

For that reason, the architecture does not inherently require browser clients or the routine verification path to make unrestricted outbound Internet calls.

A production Azure architecture could use controlled internal connectivity:

```
Compliance User
      |
      v
TTB Application
      |
      v
Private Azure Network
      |
      +--> Approved OCR / AI Service
      |
      +--> Application Data Service
```

This design allows approved cloud AI capabilities to be introduced without requiring users' browsers to connect directly to public ML endpoints.

---

## Error Handling

The prototype favors explicit, actionable errors rather than silent failures.

Examples include:

- Unsupported file format
- Image could not be decoded
- OCR service unavailable
- No readable text detected
- Required label field not detected
- Application fixture not found
- Processing timeout

Where uncertainty exists rather than a technical failure, the application returns **REVIEW** instead of manufacturing a PASS or FAIL result.

---

## Testing Strategy

Testing is organized around the verification engine rather than the UI alone.

### Unit Tests

Representative unit tests include:

- Case-insensitive brand matching
- Punctuation normalization
- Whitespace normalization
- ABV parsing
- Proof comparison
- Unit normalization
- Government-warning validation
- Missing-field behavior
- Confidence-threshold behavior
- Known fuzzy-match edge cases

### Integration Tests

Integration tests cover:

- Image-to-OCR flow
- Application-adapter loading
- End-to-end verification-pipeline execution
- Malformed inputs
- Missing application records
- OCR-service failures
- Unsupported images

### Test Labels

The evaluation set should include:

- Fully compliant labels
- Obvious application mismatches
- Capitalization differences
- Punctuation differences
- Incorrect ABV
- Incorrect proof
- Incorrect net contents
- Altered government-warning wording
- Missing government warning
- Low-quality images
- Rotated images
- Skewed images
- Glare or low-contrast examples where available

AI-generated synthetic labels may be used for prototype testing provided they do not replace validation against representative real-world image characteristics.

---

## Observability

The architecture supports structured operational telemetry for:

- Correlation ID
- Overall processing duration
- OCR duration
- Verification duration
- Result category
- Confidence indicators
- Error category

Sensitive label contents should not be written to application logs.

Observability allows the development team to assess whether the system is meeting performance and reliability objectives and to identify whether failures originate in image processing, OCR, parsing, or verification logic.

---

## Regulatory References

The prototype's supported regulatory checks are based on publicly available Alcohol and Tobacco Tax and Trade Bureau (TTB) labeling guidance.

Relevant sources include:

- TTB Distilled Spirits Labeling guidance
- TTB Mandatory Label Information guidance
- TTB Alcohol Content guidance
- TTB Net Contents guidance
- TTB Government Health Warning Statement guidance

The prototype intentionally implements only a bounded subset of labeling requirements.

For any production compliance use, regulatory rules should be independently validated, version-controlled, traceable to authoritative guidance or regulation, and reviewed when the governing requirement changes.

---

## Key Design Trade-offs

### Deterministic Rules Over Unrestricted LLM Reasoning

Compliance fields with objective comparison rules are evaluated deterministically.

This improves:

- Traceability
- Reproducibility
- Testability
- Performance
- Explainability

### Human Review Over Forced Automation

Ambiguous evidence is routed to **REVIEW** rather than forcing the system to produce an unsupported binary compliance decision.

### Adapter Boundary Over Simulated COLA Implementation

The prototype defines the minimum application-data contract needed by the verifier rather than reverse-engineering a legacy production system that is explicitly outside the assignment's scope.

### Local Processing Over Unnecessary Network Dependencies

The routine verification path favors local extraction and deterministic processing to support the stakeholder's latency target and account for restricted outbound connectivity.

### Working Core Over Broad Regulatory Coverage

The exercise prioritizes a reliable end-to-end workflow over incomplete implementation of every TTB labeling requirement.

---

## Production Evolution

A future production implementation could replace the mock application provider without modifying the core verification engine.

```
Prototype

JSON Fixture
    |
    v
Application Adapter
    |
    v
Verification Engine

Production

COLA / Authorized API
    |
    v
Application Adapter
    |
    v
Verification Engine
```

Additional production capabilities could include:

- Durable batch queues
- Secure temporary object storage
- Horizontal worker scaling
- Richer image preprocessing
- Configurable and versioned regulatory rule sets
- Approved private AI endpoints
- Authentication and authorization
- Audit history
- Workflow integration
- Operational dashboards
- Automated rule-regression testing
- Model and OCR performance monitoring

---

## Repository Structure

```
src/
  LabelVerification.Web/
  LabelVerification.Application/
  LabelVerification.Domain/
  LabelVerification.Infrastructure/

tests/
  LabelVerification.UnitTests/
  LabelVerification.IntegrationTests/

sample-data/
  applications/
  labels/

docker/
```

---

## Setup and Run Instructions

### Prerequisites

- .NET 8 SDK or later
- Docker Desktop
- Git

### 1. Clone the Repository

```
git clone <REPOSITORY_URL>
cd label-verification-app
```

### 2. Start Local OCR Services

```
docker compose up -d
```

### 3. Run the Application

```
dotnet run --project src/LabelVerification.Web
```

### 4. Open the Application

Navigate to:

```
http://localhost:5000
```

---

## Deployed Prototype

- **Application URL:** `<DEPLOYED_APPLICATION_URL>`
- **Source Repository:** `https://github.com/rawamba/ttb-ai-label-verification`

---

## Known Limitations

Current prototype limitations include:

- Limited beverage-specific regulatory coverage
- OCR accuracy dependence on image quality
- Incomplete validation of typography such as boldness or minimum type size
- No production COLA integration
- No production identity integration
- No long-term document persistence
- Limited batch-processing capability
- No autonomous final regulatory adjudication

These limitations are intentionally documented rather than hidden and represent clear areas for future production hardening.
