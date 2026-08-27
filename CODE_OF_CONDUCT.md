<!--
TTB AI-Powered Alcohol Label Verification
Repository Code of Conduct

Purpose:
- Establish a professional collaboration standard appropriate for a
  government-facing technical prototype.
- Keep discussion focused on evidence, engineering decisions, regulatory
  boundaries, and respectful technical review.
-->

# Code of Conduct

## Our Standard

This repository is intended to support thoughtful engineering collaboration around an **AI-assisted alcohol-label compliance prototype**.

Contributors, reviewers, and maintainers are expected to communicate professionally, work from evidence, and make it easy for others to understand and challenge technical decisions.

Good collaboration in this project includes:

- treating questions and technical disagreement as part of healthy engineering review;
- explaining assumptions, trade-offs, and limitations;
- separating observed evidence from inference;
- accepting constructive review without personalizing disagreement;
- protecting security-sensitive and potentially sensitive document information;
- respecting regulatory, legal, accessibility, and human-review boundaries;
- giving reviewers enough context to reproduce a change; and
- favoring clear, maintainable solutions over unnecessary complexity.

The project welcomes respectful disagreement about architecture, implementation, testing, security, performance, user experience, and regulatory interpretation.

The standard is not agreement.

The standard is **professional, evidence-based collaboration**.

---

## Unacceptable Behavior

Unacceptable behavior includes:

- harassment, intimidation, threats, or personal attacks;
- discriminatory or demeaning language;
- deliberately disruptive review behavior;
- publishing private, confidential, credential, applicant, or production information;
- knowingly misrepresenting benchmark, test, security, or compliance results;
- presenting AI-generated output as authoritative regulatory judgment when the implementation does not support that claim;
- bypassing repository security practices or encouraging others to expose secrets;
- using production applicant or COLA data in repository fixtures without authorization; and
- repeatedly ignoring documented project boundaries in a way that creates security, regulatory, or operational risk.

---

## Technical Review Expectations

Because this repository demonstrates a compliance-support workflow, technical review should distinguish among:

```text
Observed evidence
    ↓
Engineering interpretation
    ↓
Automated recommendation
    ↓
Human compliance judgment
```

Reviewers should challenge unsupported certainty.

Examples include:

- an OCR result is evidence, not regulatory authority;
- a benchmark observation is evidence, not a production SLA;
- a prototype security control is not a completed Authority to Operate;
- a technical `ERROR` is not a regulatory `FAIL`; and
- a future architecture capability should not be described as implemented.

These distinctions are part of the project's engineering culture.

---

## Security and Privacy

Do not include the following in issues, pull requests, screenshots, logs, fixtures, or comments:

- Azure access tokens;
- API keys or passwords;
- connection strings containing secrets;
- private certificates or credentials;
- production COLA records;
- non-public applicant information;
- sensitive production label submissions; or
- other information that should not be stored in a public repository.

Use synthetic repository fixtures when reproduction data is required.

Security concerns should follow the process documented in [`SECURITY.md`](SECURITY.md).

---

## Enforcement

Repository maintainers may edit, hide, close, or remove contributions that violate these standards.

Repeated or serious violations may result in restricted participation.

Security-sensitive material may be removed immediately to reduce exposure.

---

## Scope

This Code of Conduct applies to participation associated with this repository, including:

- issues;
- pull requests;
- code review;
- project discussions;
- repository-linked technical collaboration; and
- other interactions conducted in the context of this project.

---

## Project Principle

The same principle used in the application applies to collaboration:

```text
Evidence first.

Explicit assumptions.

Explainable decisions.

Human judgment where judgment is required.
```