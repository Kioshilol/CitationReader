using CitationReader.Enums;

namespace CitationReader.Models.Citation.Internal
{
    public class CitationModel
    {
        public string? Id { get; set; }

        public string? CitationNumber { get; set; }

        public string? NoticeNumber { get; set; }

        public int Provider { get; set; }

        public string? Agency { get; set; }

        public string? Address { get; set; }

        public string? Tag { get; set; }

        public string? State { get; set; }

        public DateTime? IssueDate { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public decimal Amount { get; set; }

        public string? Currency { get; set; }

        public int PaymentStatus { get; set; }

        public int FineType { get; set; }

        public string? Note { get; set; }

        public string? Link { get; set; }

        public bool IsActive { get; set; }
        
        public CitationProviderType CitationProviderType { get; set; }
    }
}
