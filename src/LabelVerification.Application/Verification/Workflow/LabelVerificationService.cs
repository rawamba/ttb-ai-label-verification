using System.Diagnostics;
using LabelVerification.Application.Abstractions;
using LabelVerification.Application.LabelIngestion;
using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Alcohol;
using LabelVerification.Application.Verification.Brand;
using LabelVerification.Application.Verification.GovernmentWarning;
using LabelVerification.Application.Verification.NetContents;
using Microsoft.Extensions.Logging;

namespace LabelVerification.Application.Verification.Workflow;

/// <summary>
/// Coordinates the end-to-end label-verification application workflow.
///
/// This class intentionally contains orchestration rather than field-specific
/// compliance logic. Each individual verifier remains responsible for its
/// own deterministic rule.
/// </summary>
public sealed class LabelVerificationService
    : ILabelVerificationService
{
    private readonly IApplicationRecordProvider _applicationRecordProvider;
    private readonly ILabelImageValidator _imageValidator;
    private readonly ILabelTextExtractor _textExtractor;
    private readonly ILabelFieldParser _fieldParser;
    private readonly IBrandNameCandidateResolver _brandNameCandidateResolver;
    private readonly IBrandNameVerifier _brandNameVerifier;
    private readonly IAlcoholValueVerifier _alcoholValueVerifier;
    private readonly INetContentsVerifier _netContentsVerifier;
    private readonly IGovernmentWarningVerifier _governmentWarningVerifier;
    private readonly IVerificationResultAggregator _resultAggregator;
    private readonly ILogger<LabelVerificationService> _logger;

    public LabelVerificationService(
    IApplicationRecordProvider applicationRecordProvider,
    ILabelImageValidator imageValidator,
    ILabelTextExtractor textExtractor,
    ILabelFieldParser fieldParser,
    IBrandNameVerifier brandNameVerifier,
    IAlcoholValueVerifier alcoholValueVerifier,
    INetContentsVerifier netContentsVerifier,
    IGovernmentWarningVerifier governmentWarningVerifier,
    IVerificationResultAggregator resultAggregator,
    ILogger<LabelVerificationService> logger,
    IBrandNameCandidateResolver? brandNameCandidateResolver = null)
    {
        ArgumentNullException.ThrowIfNull(applicationRecordProvider);
        ArgumentNullException.ThrowIfNull(imageValidator);
        ArgumentNullException.ThrowIfNull(textExtractor);
        ArgumentNullException.ThrowIfNull(fieldParser);
        ArgumentNullException.ThrowIfNull(brandNameVerifier);
        ArgumentNullException.ThrowIfNull(alcoholValueVerifier);
        ArgumentNullException.ThrowIfNull(netContentsVerifier);
        ArgumentNullException.ThrowIfNull(governmentWarningVerifier);
        ArgumentNullException.ThrowIfNull(resultAggregator);
        ArgumentNullException.ThrowIfNull(logger);

        _applicationRecordProvider =
            applicationRecordProvider;

        _imageValidator =
            imageValidator;

        _textExtractor =
            textExtractor;

        _fieldParser =
            fieldParser;

        _brandNameVerifier =
            brandNameVerifier;

        // Production composition supplies the registered resolver. The fallback
        // keeps existing direct-construction tests and benchmark hosts compatible
        // while still using exactly the same deterministic brand verifier.
        _brandNameCandidateResolver =
            brandNameCandidateResolver ??
            new BrandNameCandidateResolver(
                brandNameVerifier);

        _alcoholValueVerifier =
            alcoholValueVerifier;

        _netContentsVerifier =
            netContentsVerifier;

        _governmentWarningVerifier =
            governmentWarningVerifier;

        _resultAggregator =
            resultAggregator;

        _logger =
            logger;
    }

    /// <inheritdoc />
    public async Task<LabelVerificationSubmissionResult> VerifyAsync(
        string applicationId,
        Stream image,
        string fileName,
        string? contentType,
        long length,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        var correlationId =
            Guid.NewGuid()
                .ToString("N");

        var totalStopwatch =
            Stopwatch.StartNew();

        TimeSpan? ocrDuration = null;
        TimeSpan? verificationDuration = null;

        var normalizedApplicationId =
            string.IsNullOrWhiteSpace(applicationId)
                ? "(missing)"
                : applicationId.Trim();

        // This is a workflow-level identifier. The Web layer independently
        // supplies HTTP request correlation through CorrelationIdMiddleware.
        //
        // Keeping this identifier within the Application layer also allows
        // batch, background, CLI, or future COLA-triggered verification to
        // remain traceable without requiring an HTTP request.
        using var telemetryScope =
            _logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["VerificationCorrelationId"] =
                        correlationId
                });

        _logger.LogInformation(
            "Label verification started for application {ApplicationId}.",
            normalizedApplicationId);

        LabelVerificationSubmissionResult CreateFailure(
            string errorCode,
            string errorMessage)
        {
            totalStopwatch.Stop();

            var telemetry =
                new VerificationTelemetry
                {
                    CorrelationId = correlationId,
                    OcrDuration = ocrDuration,
                    VerificationDuration = verificationDuration,
                    TotalDuration = totalStopwatch.Elapsed
                };

            // Only operational metadata is logged. Do not add OCR text,
            // extracted field values, document bytes, or uploaded filenames.
            _logger.LogInformation(
                "Label verification completed for application {ApplicationId}. " +
                "ResultCategory {ResultCategory}; ErrorCode {ErrorCode}; " +
                "OcrDurationMs {OcrDurationMs}; " +
                "VerificationDurationMs {VerificationDurationMs}; " +
                "TotalDurationMs {TotalDurationMs}.",
                normalizedApplicationId,
                "PROCESSING_FAILURE",
                errorCode,
                ocrDuration?.TotalMilliseconds,
                verificationDuration?.TotalMilliseconds,
                telemetry.TotalDuration.TotalMilliseconds);

            return LabelVerificationSubmissionResult.Failure(
                errorCode,
                errorMessage,
                telemetry);
        }

        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return CreateFailure(
                "application_required",
                "Select an application before verifying the label.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return CreateFailure(
                "image_required",
                "Select a label image before verification.");
        }

        // Treat the upstream application record as the authoritative source
        // for expected label values.
        var applicationRecord =
            await _applicationRecordProvider.GetByIdAsync(
                applicationId.Trim(),
                cancellationToken);

        if (applicationRecord is null)
        {
            return CreateFailure(
                "application_not_found",
                $"Application '{applicationId.Trim()}' was not found.");
        }

        // Browser upload streams are not guaranteed to support seeking.
        //
        // Buffer the upload once so validation and OCR both receive the
        // complete, identical image payload. The prototype already enforces
        // a 10 MB upload limit, making bounded in-memory buffering appropriate.
        await using var bufferedImage =
            new MemoryStream(
                length > 0 &&
                length <= int.MaxValue
                    ? (int)length
                    : 0);

        await image.CopyToAsync(
            bufferedImage,
            cancellationToken);

        if (bufferedImage.Length == 0)
        {
            return CreateFailure(
                "empty_image",
                "The selected label image is empty.");
        }

        bufferedImage.Position = 0;

        var validationResult =
            await _imageValidator.ValidateAsync(
                bufferedImage,
                fileName,
                contentType,
                length,
                cancellationToken);

        if (!validationResult.IsValid)
        {
            return CreateFailure(
                validationResult.ErrorCode ??
                    "image_validation_failed",
                validationResult.ErrorMessage ??
                    "The selected label image could not be validated.");
        }

        bufferedImage.Position = 0;

        // Measure OCR at the workflow boundary instead of depending on one
        // provider's internal timing implementation. OcrResult.Duration remains
        // available separately as provider-owned diagnostic evidence.
        var ocrStopwatch =
            Stopwatch.StartNew();

        OcrResult ocrResult;

        try
        {
            ocrResult =
                await _textExtractor.ExtractAsync(
                    bufferedImage,
                    cancellationToken);
        }
        finally
        {
            ocrStopwatch.Stop();

            ocrDuration =
                ocrStopwatch.Elapsed;
        }

        // Verification duration intentionally excludes OCR. This allows the
        // performance benchmark to distinguish probabilistic AI latency from
        // deterministic application processing.
        var verificationStopwatch =
            Stopwatch.StartNew();

        //var parsedLabel =
        //    _fieldParser.Parse(
        //        ocrResult);

        //var expected =
        //    applicationRecord.ExpectedData;

        var parsedLabel =
    _fieldParser.Parse(
        ocrResult);

        var expected =
            applicationRecord.ExpectedData;

        // The parser remains evidence-only and initially selects the first plausible
        // brand candidate for backward compatibility.
        //
        // At this workflow boundary we now have both:
        // 1. observed OCR-derived candidates, and
        // 2. the authoritative expected application brand.
        //
        // Use the expected value only to resolve among candidates actually observed
        // on the uploaded label. The expected value is never substituted for OCR
        // evidence.
        var resolvedBrandName =
     _brandNameCandidateResolver.Resolve(
         expected.BrandName,
         parsedLabel.BrandName,
         parsedLabel.BrandNameCandidates,
         ocrResult,
         parsedLabel.NameAndAddress);

        parsedLabel =
            parsedLabel with
            {
                BrandName =
                    resolvedBrandName
            };


        var checks =
            new List<VerificationCheckResult>
            {
                _brandNameVerifier.Verify(
                    expected.BrandName,
                    parsedLabel.BrandName),

                _alcoholValueVerifier.VerifyAlcoholByVolume(
                    expected.AlcoholByVolume,
                    parsedLabel.AlcoholByVolume),

                _alcoholValueVerifier.VerifyProof(
                    expected.Proof,
                    parsedLabel.Proof),

                _netContentsVerifier.Verify(
                    expected.NetContents.Value,
                    expected.NetContents.Unit,
                    parsedLabel.NetContents),

                _governmentWarningVerifier.Verify(
                    parsedLabel.GovernmentWarning,
                    ocrResult)
            };

        var verification =
            _resultAggregator.Aggregate(
                checks);

        verificationStopwatch.Stop();

        verificationDuration =
            verificationStopwatch.Elapsed;

        totalStopwatch.Stop();

        var telemetry =
            new VerificationTelemetry
            {
                CorrelationId = correlationId,
                OcrDuration = ocrDuration,
                VerificationDuration = verificationDuration,
                TotalDuration = totalStopwatch.Elapsed
            };

        var resultCategory =
            verification.OverallStatus
                .ToString()
                .ToUpperInvariant();

        // Deliberately log metadata only. OCR text, uploaded image content,
        // extracted fields, addresses, warning text, and filenames are not
        // telemetry fields.
        _logger.LogInformation(
            "Label verification completed for application {ApplicationId}. " +
            "ResultCategory {ResultCategory}; " +
            "OcrDurationMs {OcrDurationMs}; " +
            "VerificationDurationMs {VerificationDurationMs}; " +
            "TotalDurationMs {TotalDurationMs}.",
            normalizedApplicationId,
            resultCategory,
            telemetry.OcrDuration?.TotalMilliseconds,
            telemetry.VerificationDuration?.TotalMilliseconds,
            telemetry.TotalDuration.TotalMilliseconds);

        return LabelVerificationSubmissionResult.Success(
            applicationRecord,
            ocrResult,
            parsedLabel,
            verification,
            telemetry);
    }
}