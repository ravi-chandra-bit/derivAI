using System.Net.Http.Json;
using System.Text.Json;
using DerivAI.Web.Models;

namespace DerivAI.Web.Services
{
    public interface IDerivAIAgentService
    {
        Task<AgentAnalysisResult> AnalyzeConfirmationAsync(string tradeId, TradeConfirmation ourConf, TradeConfirmation cptyConf);
        Task<AgentHealthResult> HealthCheckAsync();
        Task<string> TestPromptAsync(string prompt);
    }

    public class DerivAIAgentService : IDerivAIAgentService
    {
        private readonly HttpClient _http;
        private readonly ILogger<DerivAIAgentService> _logger;

        public DerivAIAgentService(IHttpClientFactory httpClientFactory, ILogger<DerivAIAgentService> logger)
        {
            _http   = httpClientFactory.CreateClient("DerivAIAgent");
            _logger = logger;
        }

        public async Task<AgentAnalysisResult> AnalyzeConfirmationAsync(string tradeId, TradeConfirmation ourConf, TradeConfirmation cptyConf)
        {
            var requestBody = new
            {
                trade_id           = tradeId,
                our_confirmation   = ourConf.ToDictionary(),
                cpty_confirmation  = cptyConf.ToDictionary()
            };

            _logger.LogInformation("Calling Python agent for trade {TradeId}", tradeId);

            var response = await _http.PostAsJsonAsync("/api/analyze-confirmation", requestBody);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AgentAnalysisResult>();
            return result ?? throw new InvalidOperationException("Empty response from agent");
        }

        public async Task<AgentHealthResult> HealthCheckAsync()
        {
            try
            {
                var response = await _http.GetAsync("/api/health");
                if (!response.IsSuccessStatusCode)
                    return new AgentHealthResult { IsConnected = false, ErrorMessage = "Agent returned error" };

                var json     = await response.Content.ReadAsStringAsync();
                var parsed   = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                return new AgentHealthResult
                {
                    IsConnected    = true,
                    ModelId        = parsed?.GetValueOrDefault("model", "Unknown") ?? "",
                    Region         = parsed?.GetValueOrDefault("region", "Unknown") ?? "",
                    VectorDatabase = parsed?.GetValueOrDefault("vector_db", "Unknown") ?? "",
                    Timestamp      = parsed?.GetValueOrDefault("timestamp", "") ?? ""
                };
            }
            catch (Exception ex)
            {
                return new AgentHealthResult { IsConnected  = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<string> TestPromptAsync(string prompt)
        {
            var body    = new { prompt };
            var resp    = await _http.PostAsJsonAsync("/api/test-prompt", body);
            return await resp.Content.ReadAsStringAsync();
        }
    }

    public interface ITradeDataService
    {
        List<TradeSummary> GetSampleTrades();
        (TradeConfirmation? Our, TradeConfirmation? Cpty) GetConfirmationPair(string tradeId);
        List<string> GetMismatchedFields(TradeConfirmation our, TradeConfirmation cpty);
    }

    public class TradeDataService : ITradeDataService
    {
        private static readonly Dictionary<string, (TradeConfirmation Our, TradeConfirmation Cpty)> _trades = new()
        {
            ["TRADE-001"] = (
                Our: new TradeConfirmation
                {
                    TradeId             = "TRADE-001",
                    ProductType         = "Interest Rate Swap (IRS)",
                    OurEntity           = "Meridian Capital Partners",
                    Counterparty        = "Axiom Global Finance",
                    CounterpartyLEI     = "254900HROIFWPRGM1V77",
                    Notional            = 50_000_000m,
                    Currency            = "USD",
                    TradeDate           = new DateTime(2025, 3, 10),
                    EffectiveDate       = new DateTime(2025, 3, 12),
                    MaturityDate        = new DateTime(2030, 3, 12),
                    FixedRatePayer      = "Meridian Capital Partners",
                    FixedRate           = 4.25m,
                    FloatingIndex       = "SOFR",
                    PaymentFrequency    = "Quarterly",
                    DayCountConvention  = "ACT/360",
                    BusinessDayConvention = "Modified Following",
                    GoverningLaw        = "New York Law",
                    DocumentationVersion = "ISDA 2002",
                    Status              = "Break"
                },
                Cpty: new TradeConfirmation
                {
                    TradeId             = "TRADE-001",
                    ProductType         = "Interest Rate Swap (IRS)",
                    OurEntity           = "Axiom Global Finance",
                    Counterparty        = "Meridian Capital Partners",
                    CounterpartyLEI     = "213800WAVVOPS85N2205",
                    Notional            = 50_500_000m,
                    Currency            = "USD",
                    TradeDate           = new DateTime(2025, 3, 10),
                    EffectiveDate       = new DateTime(2025, 3, 12),
                    MaturityDate        = new DateTime(2030, 3, 12),
                    FixedRatePayer      = "Meridian Capital Partners",
                    FixedRate           = 4.25m,
                    FloatingIndex       = "SOFR",
                    PaymentFrequency    = "Quarterly",
                    DayCountConvention  = "ACT/360",
                    BusinessDayConvention = "Modified Following",
                    GoverningLaw        = "New York Law",
                    DocumentationVersion = "ISDA 2002",
                    Status              = "Break"
                }
            ),

            ["TRADE-002"] = (
                Our: new TradeConfirmation
                {
                    TradeId             = "TRADE-002",
                    ProductType         = "Interest Rate Swap (IRS)",
                    OurEntity           = "Meridian Capital Partners",
                    Counterparty        = "Helios Investment Bank",
                    CounterpartyLEI     = "3TK20IVIUJ8J3ZU0QE75",
                    Notional            = 25_000_000m,
                    Currency            = "EUR",
                    TradeDate           = new DateTime(2025, 3, 11),
                    EffectiveDate       = new DateTime(2025, 3, 13),
                    MaturityDate        = new DateTime(2028, 3, 13),
                    FixedRatePayer      = "Helios Investment Bank",
                    FixedRate           = 4.25m,
                    FloatingIndex       = "EURIBOR 3M",
                    PaymentFrequency    = "Semi-Annual",
                    DayCountConvention  = "30/360",
                    BusinessDayConvention = "Modified Following",
                    GoverningLaw        = "English Law",
                    DocumentationVersion = "ISDA 2002",
                    Status              = "Break"
                },
                Cpty: new TradeConfirmation
                {
                    TradeId             = "TRADE-002",
                    ProductType         = "Interest Rate Swap (IRS)",
                    OurEntity           = "Helios Investment Bank",
                    Counterparty        = "Meridian Capital Partners",
                    CounterpartyLEI     = "213800WAVVOPS85N2205",
                    Notional            = 25_000_000m,
                    Currency            = "EUR",
                    TradeDate           = new DateTime(2025, 3, 11),
                    EffectiveDate       = new DateTime(2025, 3, 13),
                    MaturityDate        = new DateTime(2028, 3, 13),
                    FixedRatePayer      = "Helios Investment Bank",
                    FixedRate           = 4.35m,
                    FloatingIndex       = "EURIBOR 3M",
                    PaymentFrequency    = "Semi-Annual",
                    DayCountConvention  = "30/360",
                    BusinessDayConvention = "Modified Following",
                    GoverningLaw        = "English Law",
                    DocumentationVersion = "ISDA 2002",
                    Status              = "Break"
                }
            ),

            ["TRADE-003"] = (
                Our: new TradeConfirmation
                {
                    TradeId             = "TRADE-003",
                    ProductType         = "Interest Rate Swap (IRS)",
                    OurEntity           = "Meridian Capital Partners",
                    Counterparty        = "Vortex Structured Finance",
                    CounterpartyLEI     = "529900T8BM49AURSDO55",
                    Notional            = 100_000_000m,
                    Currency            = "GBP",
                    TradeDate           = new DateTime(2025, 3, 12),
                    EffectiveDate       = new DateTime(2025, 3, 14),
                    MaturityDate        = new DateTime(2035, 3, 14),
                    FixedRatePayer      = "Meridian Capital Partners",
                    FixedRate           = 3.75m,
                    FloatingIndex       = "SONIA",
                    PaymentFrequency    = "Quarterly",
                    DayCountConvention  = "ACT/365",
                    BusinessDayConvention = "Modified Following",
                    GoverningLaw        = "English Law",
                    DocumentationVersion = "ISDA 2002",
                    Status              = "Break"
                },
                Cpty: new TradeConfirmation
                {
                    TradeId             = "TRADE-003",
                    ProductType         = "Interest Rate Swap (IRS)",
                    OurEntity           = "Vortex Structured Finance",
                    Counterparty        = "Meridian Capital Partners",
                    CounterpartyLEI     = "213800WAVVOPS85N2205",
                    Notional            = 100_000_000m,
                    Currency            = "GBP",
                    TradeDate           = new DateTime(2025, 3, 12),
                    EffectiveDate       = new DateTime(2025, 3, 14),
                    MaturityDate        = new DateTime(2035, 3, 14),
                    FixedRatePayer      = "Meridian Capital Partners",
                    FixedRate           = 3.75m,
                    FloatingIndex       = "SONIA",
                    PaymentFrequency    = "Semi-Annual",
                    DayCountConvention  = "ACT/360",
                    BusinessDayConvention = "Modified Following",
                    GoverningLaw        = "English Law",
                    DocumentationVersion = "ISDA 2002",
                    Status              = "Break"
                }
            ),

            ["TRADE-004"] = (
                Our: new TradeConfirmation
                {
                    TradeId             = "TRADE-004",
                    ProductType         = "Interest Rate Swap (IRS)",
                    OurEntity           = "Meridian Capital Partners",
                    Counterparty        = "Cobalt Prime Securities",
                    CounterpartyLEI     = "XKZZ2JZF41MRHTR1V493",
                    Notional            = 75_000_000m,
                    Currency            = "USD",
                    TradeDate           = new DateTime(2025, 3, 13),
                    EffectiveDate       = new DateTime(2025, 3, 17),
                    MaturityDate        = new DateTime(2030, 3, 17),
                    FixedRatePayer      = "Cobalt Prime Securities",
                    FixedRate           = 4.50m,
                    FloatingIndex       = "SOFR",
                    PaymentFrequency    = "Quarterly",
                    DayCountConvention  = "ACT/360",
                    BusinessDayConvention = "Modified Following",
                    GoverningLaw        = "New York Law",
                    DocumentationVersion = "ISDA 2002",
                    Status              = "Matched"
                },
                Cpty: new TradeConfirmation
                {
                    TradeId             = "TRADE-004",
                    ProductType         = "Interest Rate Swap (IRS)",
                    OurEntity           = "Cobalt Prime Securities",
                    Counterparty        = "Meridian Capital Partners",
                    CounterpartyLEI     = "213800WAVVOPS85N2205",
                    Notional            = 75_000_000m,
                    Currency            = "USD",
                    TradeDate           = new DateTime(2025, 3, 13),
                    EffectiveDate       = new DateTime(2025, 3, 17),
                    MaturityDate        = new DateTime(2030, 3, 17),
                    FixedRatePayer      = "Cobalt Prime Securities",
                    FixedRate           = 4.50m,
                    FloatingIndex       = "SOFR",
                    PaymentFrequency    = "Quarterly",
                    DayCountConvention  = "ACT/360",
                    BusinessDayConvention = "Modified Following",
                    GoverningLaw        = "New York Law",
                    DocumentationVersion = "ISDA 2002",
                    Status              = "Matched"
                }
            )
        };

        public List<TradeSummary> GetSampleTrades()
        {
            return _trades.Select(kvp => new TradeSummary
            {
                TradeId      = kvp.Key,
                ProductType  = kvp.Value.Our.ProductType,
                Counterparty = kvp.Value.Our.Counterparty,
                Notional     = kvp.Value.Our.Notional,
                Currency     = kvp.Value.Our.Currency,
                TradeDate    = kvp.Value.Our.TradeDate,
                Status       = kvp.Value.Our.Status,
                BreakCount   = GetMismatchedFields(kvp.Value.Our, kvp.Value.Cpty).Count
            }).ToList();
        }

        public (TradeConfirmation? Our, TradeConfirmation? Cpty) GetConfirmationPair(string tradeId)
        {
            return _trades.TryGetValue(tradeId, out var pair) ? (pair.Our, pair.Cpty) : (null, null);
        }

        public List<string> GetMismatchedFields(TradeConfirmation our, TradeConfirmation cpty)
        {
            var mismatches = new List<string>();
            if (our.Notional           != cpty.Notional)           mismatches.Add("notional");
            if (our.FixedRate          != cpty.FixedRate)          mismatches.Add("fixed_rate");
            if (our.PaymentFrequency   != cpty.PaymentFrequency)   mismatches.Add("payment_frequency");
            if (our.DayCountConvention != cpty.DayCountConvention) mismatches.Add("day_count_convention");
            if (our.MaturityDate       != cpty.MaturityDate)       mismatches.Add("maturity_date");
            if (our.GoverningLaw       != cpty.GoverningLaw)       mismatches.Add("governing_law");
            return mismatches;
        }
    }

    public interface IAuditTrailService
    {
        Task LogAsync(AuditEntry entry);
        List<AuditEntry> GetRecentEntries(int count);
        List<AuditEntry> GetFilteredEntries(string? tradeId, string? severity, DateTime? from, DateTime? to);
    }

    public class InMemoryAuditTrailService : IAuditTrailService
    {
        private readonly List<AuditEntry> _log = new();
        private readonly object           _lock = new();

        public Task LogAsync(AuditEntry entry)
        {
            lock (_lock) { _log.Add(entry); }
            return Task.CompletedTask;
        }

        public List<AuditEntry> GetRecentEntries(int count)
        {
            lock (_lock)
            {
                return _log.OrderByDescending(e => e.Timestamp).Take(count).ToList();
            }
        }

        public List<AuditEntry> GetFilteredEntries(string? tradeId, string? severity, DateTime? from, DateTime? to)
        {
            lock (_lock)
            {
                var q = _log.AsQueryable();
                if (!string.IsNullOrEmpty(tradeId))  q = q.Where(e => e.TradeId.Contains(tradeId));
                if (!string.IsNullOrEmpty(severity))  q = q.Where(e => e.Severity == severity);
                if (from.HasValue)                    q = q.Where(e => e.Timestamp >= from.Value);
                if (to.HasValue)                      q = q.Where(e => e.Timestamp <= to.Value);
                return q.OrderByDescending(e => e.Timestamp).ToList();
            }
        }
    }
}
