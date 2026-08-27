<!--
TTB AI-Powered Alcohol Label Verification
Security Policy

Purpose:
- Provide a responsible reporting path for security concerns.
- Explicitly document the security boundary of this public evaluator prototype.
- Prevent sensitive information from being disclosed through public issues.
-->

# Security Policy

## Purpose

Security is part of the engineering design of the **AI-Powered Alcohol Label Verification** prototype.

The repository is public and demonstrates an evaluator-accessible architecture.

It is **not** a production Treasury authorization package, production COLA integration, or completed Authority to Operate.

Security reports should respect that distinction while still treating potential vulnerabilities seriously.

---

## Supported Version

The actively supported version is the current code on:

```text
main
```

Security fixes should target the current repository state unless a different version is explicitly identified.

---

## Reporting a Security Concern

Please **do not disclose exploitable security details in a public GitHub issue**.

If GitHub private vulnerability reporting is enabled for this repository, use the repository's private security-reporting workflow.

If private vulnerability reporting is not available, contact the repository owner privately through an appropriate trusted channel before publishing technical exploitation details.

A useful report should include, where safe:

- affected component;
- observed behavior;
- expected behavior;
- reproduction steps;
- security impact;
- whether authentication is required;
- whether sensitive information may have been exposed; and
- suggested remediation, if known.

Do not include real credentials, access tokens, applicant data, or other secrets in the report.

---

## Sensitive Information

Do not place the following in public issues, pull requests, screenshots, logs, fixtures, or commits:

```text
Azure access tokens
API keys
passwords
secret connection strings
private certificates
production applicant information
non-public COLA records
production label submissions
other sensitive government or personal data
```

If sensitive information is accidentally committed, treat it as potentially compromised and rotate or revoke the affected credential where applicable.

Removing a secret from the latest commit alone is not sufficient protection if it has already been published in Git history.

---

## Prototype Security Controls

The current evaluator prototype includes controls such as:

- HTTPS-only application access;
- Azure Managed Identity in the hosted environment;
- scoped Azure RBAC for Document Intelligence;
- no required Cognitive Services API key in application configuration;
- upload validation;
- configurable batch-size limits;
- bounded OCR concurrency;
- bounded authentication readiness;
- bounded OCR provider-operation timeout;
- randomly named temporary batch staging files;
- staged-file cleanup;
- no required long-term uploaded-label persistence;
- per-item batch fault isolation;
- structured error handling;
- non-sensitive operational telemetry; and
- deterministic compliance logic separated from AI perception.

The in-process Azure token cache is not persisted to disk.

---

## Known Prototype Boundaries

The prototype does not claim to provide:

- production federal SSO;
- complete production authorization policy;
- a Treasury production network topology;
- complete FedRAMP implementation;
- a production Authority to Operate;
- production records-management controls;
- full production PII handling;
- direct production COLA integration;
- durable distributed batch processing; or
- long-term production document storage.

Those capabilities require additional design, review, authorization, and operational controls before production use.

---

## AI and Regulatory Safety Boundary

Azure Document Intelligence is used as an evidence-extraction provider.

It is not the regulatory decision authority.

The implemented boundary is:

```text
Image
    ↓
AI / OCR evidence
    ↓
Deterministic supported rules
    ↓
PASS / REVIEW / FAIL recommendation
    ↓
Human compliance decision
```

Technical processing failures are represented separately as:

```text
ERROR
```

A technical failure should never be represented as a regulatory failure merely to produce an automated answer.

---

## Logging and Telemetry

Routine operational telemetry should not intentionally contain:

- image contents;
- OCR document text;
- parsed label values;
- Government Warning text;
- uploaded filenames;
- Azure access tokens; or
- credentials.

Synthetic repository fixture filenames may appear in benchmark evidence for reproducibility.

Production logging would require additional review for records management, access control, retention, monitoring, and incident response.

---

## Dependency and Platform Security

Security-relevant dependency updates should be evaluated for:

- exploitability;
- runtime compatibility;
- Azure SDK behavior;
- authentication impact;
- serialization behavior;
- application availability; and
- regression risk.

Do not suppress a security advisory merely to make a dependency scanner appear green.

Document the reason when a finding is accepted temporarily.

---

## Disclosure Expectations

Please allow reasonable time to:

1. reproduce the issue;
2. determine its impact;
3. develop a fix;
4. validate the fix; and
5. update affected documentation or deployment configuration.

Security discussions should prioritize reducing risk over assigning blame.

---

## Security Design Principle

```text
No secrets in source.

Least privilege for service access.

Sensitive data minimized.

Failures explicit.

Human authority preserved.
```