using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Training;

// ARCHITECTURAL INTENT: Customer Security Awareness Broadcast Engine (BRPD 12.2.1)
// Disseminates critical fraud, phishing, OTP/PIN, and ADC security warnings across digital customer channels.
public class CustomerSecurityBroadcast : AggregateRoot<Guid>
{
    public string Title { get; private set; }
    public string MessageEn { get; private set; }
    public string MessageBn { get; private set; }
    public string Category { get; private set; }
    public SecurityBroadcastChannel Channel { get; private set; }
    public bool IsActive { get; private set; }
    public DateOnly EffectiveStartDate { get; private set; }
    public DateOnly EffectiveEndDate { get; private set; }
    public long ImpressionCount { get; private set; }
    public string PublishedByUserId { get; private set; }

    #pragma warning disable CS8618
    protected CustomerSecurityBroadcast() : base(Guid.NewGuid()) { }
    #pragma warning restore CS8618

    public CustomerSecurityBroadcast(
        string title,
        string messageEn,
        string messageBn,
        string category,
        SecurityBroadcastChannel channel,
        DateOnly effectiveStartDate,
        DateOnly effectiveEndDate,
        string publishedByUserId) : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(messageEn)) throw new ArgumentException("English message is required.", nameof(messageEn));
        if (string.IsNullOrWhiteSpace(messageBn)) throw new ArgumentException("Bengali message is required.", nameof(messageBn));
        if (effectiveEndDate < effectiveStartDate)
        {
            throw new InvariantViolationException(
                "InvalidBroadcastWindowInvariant",
                "BRPD Circular 12/2024 (12.2.1)",
                $"Effective end date '{effectiveEndDate}' cannot precede start date '{effectiveStartDate}'.");
        }

        Title = title.Trim();
        MessageEn = messageEn.Trim();
        MessageBn = messageBn.Trim();
        Category = category.Trim();
        Channel = channel;
        IsActive = true;
        EffectiveStartDate = effectiveStartDate;
        EffectiveEndDate = effectiveEndDate;
        ImpressionCount = 0;
        PublishedByUserId = publishedByUserId.Trim();
    }

    public void IncrementImpression()
    {
        ImpressionCount++;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
