# Representative Label Verification Dataset

This directory contains synthetic fixtures for the TTB label-verification prototype.

The fixtures are aligned with mock application COLA-84729:

- Brand: Old Tom Distillery
- Class/type: Kentucky Straight Bourbon Whiskey
- ABV: 45%
- Proof: 90
- Net contents: 750 mL

The Government Warning uses the exact regulatory text expected by the current verifier.

Semantic fixtures intentionally mutate one field at a time. This isolates verification behavior even where the resulting combination would not represent a realistic beverage formulation.

The rotated and degraded images preserve the compliant semantic content and are intended to exercise OCR robustness.

manifest.json records the purpose and intended outcome of each sample.

These files are synthetic and contain no production, applicant, or personally identifiable information.