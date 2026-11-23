namespace Claims.Api.Models;

/// <summary>
/// A question raised by a claims officer that the claimant must answer before assessment continues.
/// Unlike <see cref="ClaimActivity"/> (append-only history), this is a stateful work item:
/// it is open until the claimant responds, then closed.
/// </summary>
public class InformationRequest
{
    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public string? Response { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }

    public bool IsAnswered => RespondedAt.HasValue;

    // Navigation properties
    public Claim? Claim { get; private set; }
    public AppUser? RequestedBy { get; private set; }

    /// <summary>
    /// Protected parameterless constructor required by Entity Framework Core.
    /// </summary>
    protected InformationRequest() { }

    public InformationRequest(Guid claimId, Guid requestedByUserId, string question)
    {
        Id = Guid.NewGuid();
        ClaimId = claimId;
        RequestedByUserId = requestedByUserId;
        Question = string.IsNullOrWhiteSpace(question)
            ? throw new ArgumentException("Question cannot be empty.", nameof(question))
            : question;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records the claimant's answer. A request can only be answered once.
    /// </summary>
    public void Respond(string response)
    {
        if (IsAnswered)
            throw new InvalidOperationException("This information request has already been answered.");
        if (string.IsNullOrWhiteSpace(response))
            throw new ArgumentException("Response cannot be empty.", nameof(response));

        Response = response;
        RespondedAt = DateTimeOffset.UtcNow;
    }
}
