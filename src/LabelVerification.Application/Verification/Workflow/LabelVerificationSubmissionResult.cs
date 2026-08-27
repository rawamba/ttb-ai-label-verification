using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Domain.Models;

namespace LabelVerification.Application.Verification.Workflow;

/// <summary>
/// Represents the outcome of processing one application/label submission.
///
/// ProcessingSucceeded describes whether the technical verification workflow
/// completed successfully. It is intentionally separate from the regulatory
/// PASS / REVIEW / FAIL status contained in VerificationResult.
/// </summary>
public sealed record LabelVerificationSubmissionResult
{
    public required bool ProcessingSucceeded { get; init; }

    /// <summary>
    /// Stable machine-readable error code when processing could not complete.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// User-safe explanation of a processing or validation failure.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public ApplicationRecord? ApplicationRecord { get; init; }

    public OcrResult? OcrResult { get; init; }

    public ParsedLabelData? ParsedLabel { get; init; }

    public VerificationResult? Verification { get; init; }

    /// <summary>
    /// Non-sensitive operational measurements for this verification attempt.
    /// </summary>
    public VerificationTelemetry? Telemetry { get; init; }

    public static LabelVerificationSubmissionResult Failure(
        string errorCode,
        string errorMessage,
        VerificationTelemetry telemetry) =>
        new()
        {
            ProcessingSucceeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Telemetry = telemetry
        };

    public static LabelVerificationSubmissionResult Success(
        ApplicationRecord applicationRecord,
        OcrResult ocrResult,
        ParsedLabelData parsedLabel,
        VerificationResult verification,
        VerificationTelemetry telemetry) =>
        new()
        {
            ProcessingSucceeded = true,
            ApplicationRecord = applicationRecord,
            OcrResult = ocrResult,
            ParsedLabel = parsedLabel,
            Verification = verification,
            Telemetry = telemetry
        };
}