using CitationReader.Enums;

namespace CitationReader.Common;

public static class PaymentHelper
{
    public static PaymentStatus GetStatus(string? status)
    {
        if (string.IsNullOrEmpty(status))
        {
            return PaymentStatus.Unknown;
        }

        return status.ToUpper() switch
        {
            "OPEN" or "UNPAID" => PaymentStatus.New,
            "PAID" => PaymentStatus.Paid,
            "VOID" => PaymentStatus.Paid,
            "PENDING" => PaymentStatus.Paid,
            "OVERDUE" => PaymentStatus.New,
            "CLOSED VOID" => PaymentStatus.Paid,
            "CLOSED WARNING" => PaymentStatus.Paid,
            "CLOSED PAID" => PaymentStatus.Paid,
            _ => PaymentStatus.Unknown
        };
    }
}
