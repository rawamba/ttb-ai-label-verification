using LabelVerification.Application.Abstractions;
using LabelVerification.Application.LabelIngestion;
using LabelVerification.Application.LabelUnderstanding;
using LabelVerification.Application.Verification;
using LabelVerification.Application.Verification.Alcohol;
using LabelVerification.Application.Verification.Brand;
using LabelVerification.Application.Verification.GovernmentWarning;
using LabelVerification.Application.Verification.NetContents;

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
    private readonly IBrandNameVerifier _brandNameVerifier;
    private readonly IAlcoholValueVerifier _alcoholValueVerifier;
    private readonly INetContentsVerifier _netContentsVerifier;
    private readonly IGovernmentWarningVerifier _governmentWarningVerifier;
    private readonly IVerificationResultAggregator _resultAggregator;

    public LabelVerificationService(
        IApplicationRecordProvider applicationRecordProvider,
        ILabelImageValidator imageValidator,
        ILabelTextExtractor textExtractor,
        ILabelFieldParser fieldParser,
        IBrandNameVerifier brandNameVerifier,
        IAlcoholValueVerifier alcoholValueVerifier,
        INetContentsVerifier netContentsVerifier,
        IGovernmentWarningVerifier governmentWarningVerifier,
        IVerificationResultAggregator resultAggregator)
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

        _applicationRecordProvider = applicationRecordProvider;
        _imageValidator = imageValidator;
        _textExtractor = textExtractor;
        _fieldParser = fieldParser;
        _brandNameVerifier = brandNameVerifier;
        _alcoholValueVerifier = alcoholValueVerifier;
        _netContentsVerifier = netContentsVerifier;
        _governmentWarningVerifier = governmentWarningVerifier;
        _resultAggregator = resultAggregator;
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

        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return LabelVerificationSubmissionResult.Failure(
                "application_required",
                "Select an application before verifying the label.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return LabelVerificationSubmissionResult.Failure(
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
            return LabelVerificationSubmissionResult.Failure(
                "application_not_found",
                $"Application '{applicationId.Trim()}' was not found.");
        }

        // Browser upload streams are not guaranteed to support seeking.
        //
        // Buffer the upload once so validation and OCR both receive the
        // complete, identical image payload. The prototype already enforces
        // a 10 MB upload limit, making bounded in-memory buffering appropriate.
        //
        // A future batch or high-volume production workflow could replace
        // this with temporary or blob-backed storage without changing the
        // downstream validation/OCR abstractions.
        await using var bufferedImage =
            new MemoryStream(
                length > 0 &&
                length <= int.MaxValue
                    ? (int)length
                    : 0);

        await image.CopyToAsync(
            bufferedImage,
            cancellationToken);

        // Reject an unexpectedly empty stream before invoking validation or
        // an external AI dependency.
        if (bufferedImage.Length == 0)
        {
            return LabelVerificationSubmissionResult.Failure(
                "empty_image",
                "The selected label image is empty.");
        }

        // Validation must start at the beginning of the buffered payload.
        bufferedImage.Position = 0;

        // Reject invalid images before invoking the AI/OCR provider.
        var validationResult =
            await _imageValidator.ValidateAsync(
                bufferedImage,
                fileName,
                contentType,
                length,
                cancellationToken);

        if (!validationResult.IsValid)
        {
            return LabelVerificationSubmissionResult.Failure(
                validationResult.ErrorCode ??
                    "image_validation_failed",
                validationResult.ErrorMessage ??
                    "The selected label image could not be validated.");
        }

        // Validation may inspect signature bytes. Because this workflow owns
        // a seekable buffer, explicitly rewind before handing it to OCR rather
        // than depending on validator implementation details.
        bufferedImage.Position = 0;

        // AI is used for perception: extracting text, confidence, and
        // available typography evidence from the physical label.
        var ocrResult =
            await _textExtractor.ExtractAsync(
                bufferedImage,
                cancellationToken);

        // Parsing converts OCR evidence into provider-neutral structured
        // fields without making compliance decisions.
        var parsedLabel =
            _fieldParser.Parse(
                ocrResult);

        var expected =
            applicationRecord.ExpectedData;

        // Objective comparison rules remain deterministic and independent
        // from the OCR provider.
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

        return LabelVerificationSubmissionResult.Success(
            applicationRecord,
            ocrResult,
            parsedLabel,
            verification);
    }
}