# ADR 0002: Use Hybrid Verification with Deterministic Rules and Human Review

- **Status:** Accepted
- **Date:** 2026-08-25

## Context

Alcohol-label verification involves both perception problems and objective compliance checks.

Some inputs may be difficult to read because of skew, glare, lighting, image quality, or typography. These situations benefit from OCR or AI-assisted perception.

Other checks, such as alcohol by volume, proof, net contents, and supported regulatory wording, have objective comparison semantics and do not require generative AI reasoning.

The system must also avoid creating false certainty when evidence is ambiguous.

## Decision

The verification strategy will follow this principle:

> **AI for perception and ambiguity; deterministic rules for objective compliance; human judgment for final compliance decisions.**

AI-assisted or probabilistic processing may be used for:

- OCR and text extraction.
- Image interpretation.
- Classification.
- Ambiguous evidence.

Deterministic logic will be preferred for:

- ABV comparison.
- Proof comparison.
- Net-content comparison.
- Supported Government Warning validation.
- Status aggregation.

The system will produce three result categories:

- `PASS`
- `REVIEW`
- `FAIL`

`REVIEW` is used when available evidence is insufficient for a defensible automated determination.

The compliance agent remains the final decision authority.

## Consequences

### Positive

- Objective checks remain explainable and reproducible.
- AI is not treated as the final regulatory authority.
- Low-confidence OCR does not automatically become a compliance failure.
- Agents receive explicit evidence for why a result was produced.
- The architecture supports human judgment where nuance matters.

### Negative

- Multiple verification strategies must be implemented.
- Thresholds for normalization, fuzzy comparison, and confidence require careful testing.
- Some cases will intentionally remain unresolved by automation.

## Alternatives Considered

### Generative AI for All Verification

Rejected because objective compliance checks do not require probabilistic reasoning and should remain deterministic where possible.

### Fully Deterministic Processing

Rejected because label images may contain poor-quality or visually ambiguous evidence that requires OCR or probabilistic interpretation.

### Binary PASS/FAIL Only

Rejected because ambiguous evidence should be routed to human review rather than converted into unsupported certainty.