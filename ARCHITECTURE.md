# DerivAI Architecture

## System Overview

DerivAI is a hybrid architecture application combining ASP.NET Core 8 MVC frontend with a Python FastAPI backend powered by LangGraph and Amazon Bedrock.

## Component Details

### 1. ASP.NET Core Frontend

**Purpose**: User interface for operations analysts to review trade confirmations and AI analysis results.

**Key Components**:
- **Controllers/DerivAIController.cs**: Handles HTTP requests, orchestrates service calls
- **Models/Models.cs**: Data transfer objects (DTOs) for trade confirmations and analysis results
- **Models/Services.cs**: Service layer with HTTP client to Python backend
- **Views/**: Razor views with Bootstrap 5 UI

**Design Patterns**:
- MVC (Model-View-Controller)
- Dependency Injection
- Repository pattern for trade data
- Service layer for external API calls

### 2. Python FastAPI Backend

**Purpose**: AI agent orchestration using LangGraph for multi-step reasoning.

**Key Components**:
- **api_bridge.py**: FastAPI REST endpoints
- **deriv_ai_agent.py**: LangGraph agent with 4-node workflow

**LangGraph Workflow**:

```
┌─────────────────┐
│  detect_breaks  │  Node 1: Deterministic field comparison
└────────┬────────┘
         │
         ▼
    ┌────────┐
    │ breaks?│  Conditional routing
    └───┬─┬──┘
        │ │
    Yes │ │ No
        │ └──────────────────────┐
        ▼                        │
┌──────────────────────┐         │
│ retrieve_isda_context│  Node 2: RAG from OpenSearch
└──────────┬───────────┘         │
           │                     │
           ▼                     │
┌──────────────────────┐         │
│ explain_breaks_llm   │  Node 3: Claude analysis
└──────────┬───────────┘         │
           │                     │
           ▼                     ▼
      ┌────────────────────────┐
      │  write_audit_trail     │  Node 4: DynamoDB logging
      └────────────────────────┘
```

### 3. AWS Services

#### Amazon Bedrock (Claude 3 Haiku)
- **Purpose**: LLM for break explanation and resolution recommendations
- **Model**: anthropic.claude-3-haiku-20240307-v1:0
- **Input**: Trade confirmations + detected breaks + ISDA context
- **Output**: Structured JSON with explanations and recommended actions

#### OpenSearch Serverless (Optional)
- **Purpose**: Vector store for ISDA rules (RAG)
- **Index**: isda-rules
- **Embeddings**: Amazon Titan Embed Text v2
- **Query**: Semantic search for relevant regulatory context

#### DynamoDB
- **Purpose**: Audit trail for compliance
- **Table**: DerivAI_AuditTrail
- **Schema**:
  - PK: audit_id (String)
  - SK: timestamp (String)
  - Attributes: trade_id, break_count, resolution, llm_explanation

## Data Flow

### Trade Analysis Flow

1. **User Action**: Analyst clicks "AI Analyze" on dashboard
2. **Frontend**: POST to /DerivAI/AnalyzeBreak with tradeId
3. **Controller**: Retrieves trade pair from TradeDataService
4. **HTTP Call**: POST to Python backend /api/analyze-confirmation
5. **Field Mapping**: C# field names → Python field names
6. **LangGraph Execution**:
   - Node 1: Detect breaks (pure Python logic)
   - Node 2: RAG retrieval from OpenSearch (if breaks exist)
   - Node 3: Claude LLM analysis with ISDA context
   - Node 4: Write audit record to DynamoDB
7. **Response Mapping**: Python response → C# AgentAnalysisResult
8. **View Rendering**: BreakAnalysis.cshtml displays results

### Field Name Mapping

| C# Frontend | Python Backend |
|-------------|----------------|
| notional | notional_amount |
| maturity_date | termination_date |
| floating_index | floating_rate_index |

## Security Architecture

### Authentication & Authorization
- AWS IAM roles for service-to-service auth
- No hardcoded credentials
- SigV4 signing for OpenSearch Serverless

### Data Protection
- HTTPS enforced in production
- No PII in logs or audit trail
- Trade data anonymized (fictional counterparty names)

### Rate Limiting
- Bedrock: 10 requests/second (configurable)
- FastAPI: 100 requests/minute per IP

## Scalability Considerations

### Horizontal Scaling
- **Frontend**: Multiple ECS tasks behind ALB
- **Backend**: Multiple FastAPI instances (stateless)
- **Bedrock**: Auto-scales (managed service)
- **OpenSearch**: OCU-based scaling

### Performance Optimization
- HTTP connection pooling (HttpClientFactory)
- Async/await throughout stack
- LangGraph streaming for real-time updates
- DynamoDB batch writes for audit trail

## Monitoring & Observability

### Metrics
- CloudWatch: Request latency, error rates
- Bedrock: Token usage, model invocation count
- DynamoDB: Read/write capacity units

### Logging
- Structured logging (JSON format)
- Log levels: INFO, WARNING, ERROR
- Correlation IDs for request tracing

### Alerting
- High error rate (>5%)
- Bedrock throttling
- DynamoDB capacity exceeded

## Deployment Patterns

### Development
- Local: ASP.NET (localhost:5001) + Python (localhost:8000)
- Docker Compose for full stack

### Staging
- ECS Fargate (2 tasks)
- RDS for persistent audit trail
- OpenSearch Serverless (1 OCU)

### Production
- ECS Fargate (4+ tasks, auto-scaling)
- Multi-AZ deployment
- CloudFront CDN for static assets
- Route 53 for DNS

## Disaster Recovery

### Backup Strategy
- DynamoDB: Point-in-time recovery enabled
- OpenSearch: Daily snapshots to S3
- Application code: Git repository

### RTO/RPO
- RTO: 15 minutes (ECS task restart)
- RPO: 5 minutes (DynamoDB PITR)

## Future Enhancements

1. **Real-time Streaming**: WebSocket for live analysis updates
2. **Multi-tenancy**: Separate data per organization
3. **Advanced RAG**: Hybrid search (keyword + semantic)
4. **Model Fine-tuning**: Custom Claude model on ISDA corpus
5. **Workflow Automation**: Auto-resolve low-severity breaks
