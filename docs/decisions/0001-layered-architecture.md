# ADR 0001: Use a Layered Application Architecture

- **Status:** Accepted
- **Date:** 2026-08-25

## Context

The prototype must combine a web user experience, verification logic, OCR and external-data integrations, and regulatory decision support.

Placing these concerns in a single project would make verification rules difficult to test independently and would tightly couple business logic to UI and infrastructure technologies.

The prototype must also preserve clean seams for future COLA and Azure integrations without requiring the verification workflow to be rewritten.

## Decision

The application will use four primary projects:

- `LabelVerification.Domain`
- `LabelVerification.Application`
- `LabelVerification.Infrastructure`
- `LabelVerification.Web`

Responsibilities are separated as follows:

- **Domain** contains core business concepts, verification results, value objects, and pure rules.
- **Application** contains use-case orchestration and interfaces required by the verification workflow.
- **Infrastructure** implements external concerns such as OCR and application-data providers.
- **Web** provides the Blazor user experience and acts as the application composition root.

The intended dependency direction is:

```text
Domain
  ↑
Application
  ↑
Infrastructure

Application
  ↑
Web

Infrastructure
  ↑
Web
```

The Domain project remains independent of all other projects.

## Consequences

### Positive

- Verification logic can be tested independently of the UI.
- OCR implementations can be replaced without rewriting workflow logic.
- Prototype JSON data can later be replaced by a COLA adapter.
- Infrastructure-specific technologies remain outside the Domain layer.
- The Web project remains focused on presentation and composition.

### Negative

- The solution contains more projects than a minimal prototype.
- Interfaces and dependency-injection registration introduce some additional structure.

## Alternatives Considered

### Single Web Project

Rejected because it would encourage business rules, provider integrations, and UI logic to become tightly coupled.

### Separate Microservices

Rejected because the prototype does not require distributed deployment complexity. A modular monolith provides sufficient separation while remaining fast to build and deploy.