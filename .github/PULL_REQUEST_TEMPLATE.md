<!--
TTB AI-Powered Alcohol Label Verification
Pull Request Template

Reviewer guidance:
- Keep implementation claims aligned with actual prototype behavior.
- Distinguish technical ERROR from regulatory PASS / REVIEW / FAIL.
- Distinguish AI evidence extraction from deterministic verification.
- Distinguish measured evidence from assumptions or future architecture.
-->

## Summary

<!--
What changed and why?
Keep this focused on the engineering problem solved by this PR.
-->

Describe the change.

## Architecture / Design Impact

<!--
Explain relevant boundaries or trade-offs.

Examples:
- Application vs Infrastructure responsibility
- AI/OCR perception vs deterministic compliance logic
- single-label vs batch orchestration
- adapter boundary for application data
- authentication/security implications
-->

Describe architecture impact or write `None`.

## Regulatory Behavior

<!--
Does this PR change PASS / REVIEW / FAIL behavior?

If yes, identify:
- affected field/rule;
- expected behavior;
- deterministic vs judgment-based behavior;
- supporting tests or regulatory reference.

Remember: technical ERROR is not regulatory FAIL.
-->

- [ ] Changes regulatory verification behavior
- [ ] Does not change regulatory verification behavior

Details:

## Security / Privacy

<!--
Confirm that the change does not introduce secrets, production applicant data,
OCR document contents in routine telemetry, or unnecessary sensitive data
retention.
-->

- [ ] No credentials, tokens, or secret values were added.
- [ ] No production applicant or non-public COLA data was added.
- [ ] Logging/telemetry remains non-sensitive.
- [ ] Security implications were reviewed.

Details:

## Testing

<!--
Include exact validation performed.
-->

```text
Release build:
Deterministic tests:
Live OCR test, if applicable:
Other validation:
```

## Performance Evidence

<!--
Complete this section when the change can affect OCR latency, throughput,
batch behavior, startup behavior, or external-service interaction.

Keep per-label latency separate from whole-batch wall-clock timing.
-->

Not applicable, or:

```text
Environment:
Sample size:
Concurrency:
Median:
P95:
Worst:
Technical errors:
```

## User Experience

<!--
Describe visible workflow changes, accessibility considerations, error
handling, and human-review behavior when applicable.
-->

Describe UX impact or write `None`.

## Documentation

- [ ] README remains accurate.
- [ ] Architecture documentation remains accurate.
- [ ] Relevant ADRs were reviewed.
- [ ] Known limitations remain explicit.
- [ ] Future capabilities are not described as already implemented.

## Reviewer Checklist

- [ ] The change is focused and understandable.
- [ ] Automated behavior is explainable.
- [ ] Ambiguous evidence becomes REVIEW where appropriate.
- [ ] Technical failures remain separate from regulatory outcomes.
- [ ] Relevant tests cover the change.
- [ ] No sensitive information is exposed.
- [ ] Performance claims are backed by measurement where applicable.
- [ ] Human compliance authority is preserved.

## Work Item

<!--
Example:
AB#258
-->

AB#