using LabelVerification.Application.Exceptions;
using LabelVerification.Domain.Models;

namespace LabelVerification.Application.Abstractions;

/// <summary>
/// Provides application data required by the label-verification workflow.
///
/// The Application layer owns this abstraction because verification needs
/// application data but should not depend on how that data is obtained.
/// Implementations may read local JSON fixtures in the prototype or retrieve
/// data from an upstream COLA integration in a future production environment.
/// </summary>
public interface IApplicationRecordProvider
{
    /// <summary>
    /// Retrieves an application record by its upstream application identifier.
    /// </summary>
    /// <param name="applicationId">
    /// Identifier of the application record to retrieve.
    /// </param>
    /// <param name="cancellationToken">
    /// Allows the calling workflow to cancel the operation.
    /// </param>
    /// <returns>
    /// The matching <see cref="ApplicationRecord"/> when found;
    /// otherwise <see langword="null"/> when no record exists.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="applicationId"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="InvalidApplicationRecordException">
    /// Thrown when an application record exists but cannot be safely interpreted
    /// as valid application data.
    /// </exception>
    Task<ApplicationRecord?> GetByIdAsync(
        string applicationId,
        CancellationToken cancellationToken = default);
}