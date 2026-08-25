# ADR 0003: Treat COLA as an Upstream System Behind an Adapter Boundary

- **Status:** Accepted
- **Date:** 2026-08-25

## Context

The verification workflow requires expected application information such as brand name, class/type, alcohol content, proof, and net contents.

The prototype requirements explicitly exclude direct integration with the existing COLA system.

Coupling the verification engine directly to COLA implementation details would increase prototype scope and make future integration changes more difficult.

## Decision

COLA will be treated as an upstream system of record.

The Application layer will define a narrow provider interface for retrieving only the application data required by verification.

Conceptually:

```text
Verification Workflow
        |
        v
IApplicationRecordProvider
        |
        +---- JsonApplicationRecordProvider
        |
        +---- Future ColaApplicationRecordProvider
```

The prototype will use local JSON fixtures to implement this contract.

Example application data:

```json
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

The Government Warning is treated as a regulatory rule rather than application-specific expected data.

## Consequences

### Positive

- The prototype remains independent of COLA implementation details.
- Future COLA integration can be introduced without rewriting verification logic.
- Development and testing can proceed using deterministic local fixtures.
- The data contract clearly identifies which upstream information the verifier actually requires.

### Negative

- The prototype does not demonstrate live COLA connectivity.
- Mock data may not represent every production data nuance.
- A production adapter will still require authentication, error handling, mapping, and operational controls.

## Alternatives Considered

### Direct COLA Integration

Rejected because it is explicitly outside prototype scope and would introduce unnecessary schedule and environment dependencies.

### Embed Expected Values in UI Code

Rejected because it would tightly couple application data to presentation logic and provide no clean production integration path.