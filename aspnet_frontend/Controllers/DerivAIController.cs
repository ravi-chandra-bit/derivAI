using Microsoft.AspNetCore.Mvc;
using DerivAI.Web.Models;
using DerivAI.Web.Services;
using System.Text.Json;

namespace DerivAI.Web.Controllers
{
    public class DerivAIController : Controller
    {
        private readonly IDerivAIAgentService   _agentService;
        private readonly ITradeDataService      _tradeDataService;
        private readonly IAuditTrailService     _auditService;
        private readonly ILogger<DerivAIController> _logger;

        public DerivAIController(
            IDerivAIAgentService   agentService,
            ITradeDataService      tradeDataService,
            IAuditTrailService     auditService,
            ILogger<DerivAIController> logger)
        {
            _agentService     = agentService;
            _tradeDataService = tradeDataService;
            _auditService     = auditService;
            _logger           = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new DashboardViewModel
            {
                SampleTrades      = _tradeDataService.GetSampleTrades(),
                RecentAuditEntries = _auditService.GetRecentEntries(10)
            };
            return View(model);
        }

        [HttpGet]
        public IActionResult Confirmation([FromQuery] string? tradeId)
        {
            if (string.IsNullOrEmpty(tradeId))
            {
                TempData["Error"] = "Trade ID is required.";
                return RedirectToAction(nameof(Index));
            }

            var (ourConf, cptyConf) = _tradeDataService.GetConfirmationPair(tradeId);

            if (ourConf == null || cptyConf == null)
            {
                TempData["Error"] = $"Trade {tradeId} not found.";
                return RedirectToAction(nameof(Index));
            }

            var model = new ConfirmationViewModel
            {
                TradeId              = tradeId,
                OurConfirmation      = ourConf,
                CounterpartyConf     = cptyConf,
                HighlightedFields    = _tradeDataService.GetMismatchedFields(ourConf, cptyConf)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnalyzeBreak(string tradeId)
        {
            _logger.LogInformation("Break analysis requested for trade {TradeId}", tradeId);

            var (ourConf, cptyConf) = _tradeDataService.GetConfirmationPair(tradeId);

            if (ourConf == null || cptyConf == null)
            {
                TempData["Error"] = $"Trade {tradeId} not found.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var analysisResult = await _agentService.AnalyzeConfirmationAsync(
                    tradeId, ourConf, cptyConf);

                await _auditService.LogAsync(new AuditEntry
                {
                    TradeId      = tradeId,
                    Action       = "BreakAnalysis",
                    Severity     = analysisResult.Severity,
                    PerformedBy  = "DerivAI-Agent",
                    Details      = $"Analysis complete. {analysisResult.DiscrepancyCount} discrepancies. " +
                                   $"Severity: {analysisResult.Severity}. " +
                                   $"Processing: {analysisResult.ProcessingTimeMs}ms"
                });

                var model = new BreakAnalysisViewModel
                {
                    TradeId           = tradeId,
                    OurConfirmation   = ourConf,
                    CptyConfirmation  = cptyConf,
                    AnalysisResult    = analysisResult
                };

                return View("BreakAnalysis", model);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to connect to Python agent for trade {TradeId}", tradeId);
                TempData["Error"] = "Could not connect to the AI agent. Ensure the Python service is running on port 8000.";
                return RedirectToAction(nameof(Confirmation), new { tradeId });
            }
        }

        [HttpGet]
        public IActionResult AuditTrail(
            string? tradeId   = null,
            string? severity  = null,
            DateTime? fromDate = null,
            DateTime? toDate   = null)
        {
            var entries = _auditService.GetFilteredEntries(tradeId, severity, fromDate, toDate);
            var model = new AuditTrailViewModel
            {
                Entries      = entries,
                FilterTradeId = tradeId,
                FilterSeverity = severity
            };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> TestBedrock()
        {
            var result = await _agentService.HealthCheckAsync();
            var model = new BedrockTestViewModel
            {
                IsConnected    = result.IsConnected,
                ModelId        = result.ModelId,
                Region         = result.Region,
                VectorDatabase = result.VectorDatabase,
                Timestamp      = result.Timestamp,
                ErrorMessage   = result.ErrorMessage
            };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> HealthCheck()
        {
            var result = await _agentService.HealthCheckAsync();
            return Json(new { isConnected = result.IsConnected });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestBedrockPrompt(string testPrompt)
        {
            var response = await _agentService.TestPromptAsync(testPrompt);
            return Json(new { response = response });
        }
    }
}
