using System.Text.Json.Serialization;

namespace DerivAI.Web.Models
{
    public class TradeConfirmation
    {
        public string TradeId              { get; set; } = "";
        public string ProductType          { get; set; } = "";
        public string OurEntity            { get; set; } = "";
        public string Counterparty         { get; set; } = "";
        public string CounterpartyLEI      { get; set; } = "";
        public decimal Notional            { get; set; }
        public string Currency             { get; set; } = "";
        public DateTime TradeDate          { get; set; }
        public DateTime EffectiveDate      { get; set; }
        public DateTime MaturityDate       { get; set; }
        public string FixedRatePayer       { get; set; } = "";
        public decimal FixedRate           { get; set; }
        public string FloatingIndex        { get; set; } = "";
        public string PaymentFrequency     { get; set; } = "";
        public string DayCountConvention   { get; set; } = "";
        public string BusinessDayConvention { get; set; } = "";
        public string GoverningLaw         { get; set; } = "";
        public string DocumentationVersion { get; set; } = "";
        public string Status               { get; set; } = "";

        public Dictionary<string, object> ToDictionary() => new()
        {
            ["trade_id"]              = TradeId,
            ["product_type"]          = ProductType,
            ["our_entity"]            = OurEntity,
            ["counterparty"]          = Counterparty,
            ["counterparty_lei"]      = CounterpartyLEI,
            ["notional"]              = Notional,
            ["currency"]              = Currency,
            ["trade_date"]            = TradeDate.ToString("yyyy-MM-dd"),
            ["effective_date"]        = EffectiveDate.ToString("yyyy-MM-dd"),
            ["maturity_date"]         = MaturityDate.ToString("yyyy-MM-dd"),
            ["fixed_rate_payer"]      = FixedRatePayer,
            ["fixed_rate"]            = FixedRate,
            ["floating_index"]        = FloatingIndex,
            ["payment_frequency"]     = PaymentFrequency,
            ["day_count_convention"]  = DayCountConvention,
            ["business_day_convention"] = BusinessDayConvention,
            ["governing_law"]         = GoverningLaw,
            ["documentation_version"] = DocumentationVersion
        };
    }

    public class TradeSummary
    {
        public string TradeId      { get; set; } = "";
        public string ProductType  { get; set; } = "";
        public string Counterparty { get; set; } = "";
        public decimal Notional    { get; set; }
        public string Currency     { get; set; } = "";
        public DateTime TradeDate  { get; set; }
        public string Status       { get; set; } = "";
        public int BreakCount      { get; set; }
    }

    public class AgentAnalysisResult
    {
        [JsonPropertyName("trade_id")]
        public string TradeId { get; set; } = "";

        [JsonPropertyName("has_breaks")]
        public bool HasBreaks { get; set; }

        [JsonPropertyName("discrepancy_count")]
        public int DiscrepancyCount { get; set; }

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "";

        [JsonPropertyName("break_explanation")]
        public string BreakExplanation { get; set; } = "";

        [JsonPropertyName("resolution_steps")]
        public List<string> ResolutionSteps { get; set; } = new();

        [JsonPropertyName("audit_trail")]
        public List<string> AuditTrail { get; set; } = new();

        [JsonPropertyName("processing_time_ms")]
        public int ProcessingTimeMs { get; set; }

        public string SeverityBadgeClass => Severity switch
        {
            "Critical" => "badge bg-danger",
            "High"     => "badge bg-warning text-dark",
            "Medium"   => "badge bg-info text-dark",
            "Low"      => "badge bg-success",
            _          => "badge bg-secondary"
        };
    }

    public class AgentHealthResult
    {
        public bool     IsConnected    { get; set; }
        public string   ModelId        { get; set; } = "";
        public string   Region         { get; set; } = "";
        public string   VectorDatabase { get; set; } = "";
        public string   Timestamp      { get; set; } = "";
        public string?  ErrorMessage   { get; set; }
    }

    public class DashboardViewModel
    {
        public List<TradeSummary> SampleTrades       { get; set; } = new();
        public List<AuditEntry>   RecentAuditEntries { get; set; } = new();
        public int TotalTrades  => SampleTrades.Count;
        public int TotalBreaks  => SampleTrades.Count(t => t.Status == "Break");
        public int TotalMatched => SampleTrades.Count(t => t.Status == "Matched");
    }

    public class ConfirmationViewModel
    {
        public string TradeId                    { get; set; } = "";
        public TradeConfirmation OurConfirmation      { get; set; } = new();
        public TradeConfirmation CounterpartyConf     { get; set; } = new();
        public List<string> HighlightedFields         { get; set; } = new();
    }

    public class BreakAnalysisViewModel
    {
        public string TradeId                    { get; set; } = "";
        public TradeConfirmation OurConfirmation { get; set; } = new();
        public TradeConfirmation CptyConfirmation{ get; set; } = new();
        public AgentAnalysisResult AnalysisResult{ get; set; } = new();
    }

    public class AuditTrailViewModel
    {
        public List<AuditEntry> Entries         { get; set; } = new();
        public string? FilterTradeId            { get; set; }
        public string? FilterSeverity           { get; set; }
    }

    public class BedrockTestViewModel
    {
        public bool    IsConnected    { get; set; }
        public string  ModelId        { get; set; } = "";
        public string  Region         { get; set; } = "";
        public string  VectorDatabase { get; set; } = "";
        public string  Timestamp      { get; set; } = "";
        public string? ErrorMessage   { get; set; }
    }

    public class AuditEntry
    {
        public Guid     Id          { get; set; } = Guid.NewGuid();
        public string   TradeId     { get; set; } = "";
        public string   Action      { get; set; } = "";
        public string   Severity    { get; set; } = "";
        public string   PerformedBy { get; set; } = "";
        public string   Details     { get; set; } = "";
        public DateTime Timestamp   { get; set; } = DateTime.UtcNow;

        public string SeverityBadgeClass => Severity switch
        {
            "Critical" => "badge bg-danger",
            "High"     => "badge bg-warning text-dark",
            "Medium"   => "badge bg-info text-dark",
            "Low"      => "badge bg-success",
            _          => "badge bg-secondary"
        };
    }
}
