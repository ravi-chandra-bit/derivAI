# DerivAI - AI-Powered Derivatives Trade Confirmation Reconciliation

An intelligent trade confirmation matching system that uses Amazon Bedrock (Claude), LangGraph, and RAG to automatically detect, explain, and resolve discrepancies in OTC derivatives trade confirmations.

##  Overview

DerivAI automates the manual, error-prone process of reconciling trade confirmations between counterparties in derivatives trading. The system:

- **Detects** field-level mismatches between internal and counterparty confirmations
- **Explains** each break using AI with ISDA regulatory context via RAG
- **Recommends** resolution actions based on market conventions and regulatory requirements
- **Audits** all analysis activities for compliance and operational review

## Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────────┐
│                     ASP.NET Core 8 Frontend                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │  Dashboard   │  │ Confirmation │  │ Break        │          │
│  │  (MVC)       │  │ Viewer       │  │ Analysis     │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└────────────────────────────┬────────────────────────────────────┘
                             │ HTTP/JSON
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                   Python FastAPI Backend                         │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              LangGraph Agent Pipeline                     │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ │   │
│  │  │ Detect   │→ │   RAG    │→ │   LLM    │→ │  Audit   │ │   │
│  │  │ Breaks   │  │ Retrieval│  │ Analysis │  │  Trail   │ │   │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘ │   │
│  └──────────────────────────────────────────────────────────┘   │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                        AWS Services                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │   Bedrock    │  │  OpenSearch  │  │  DynamoDB    │          │
│  │   (Claude)   │  │  Serverless  │  │ (Audit Log)  │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└─────────────────────────────────────────────────────────────────┘
```

### Technology Stack

**Frontend:**
- ASP.NET Core 8 MVC
- Bootstrap 5.3
- Razor Views

**Backend:**
- Python 3.11+
- FastAPI
- LangChain & LangGraph
- Amazon Bedrock (Claude 3 Haiku)
- OpenSearch Serverless (RAG vector store)
- DynamoDB (audit trail)

## Quick Start

### Prerequisites

- **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Python 3.11+** - [Download](https://www.python.org/downloads/)
- **AWS Account** with:
  - Bedrock model access (Claude 3 Haiku)
  - OpenSearch Serverless collection (optional for RAG)
  - DynamoDB table
- **AWS CLI** configured with credentials

### Local Development Setup

#### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/DerivAI.git
cd DerivAI
```

#### 2. Configure Python Backend

```bash
cd python_agents

# Create virtual environment
python -m venv venv

# Activate virtual environment
# Windows:
venv\Scripts\activate
# macOS/Linux:
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt

# Configure environment variables
cp .env.example .env
# Edit .env with your AWS settings:
# - AWS_REGION=us-east-1
# - BEDROCK_MODEL_ID=anthropic.claude-3-haiku-20240307-v1:0
# - OPENSEARCH_URL=https://your-endpoint.region.aoss.amazonaws.com
# - OPENSEARCH_INDEX=isda-rules
# - AUDIT_TABLE_NAME=DerivAI_AuditTrail

# Start the FastAPI server
uvicorn api_bridge:app --host 0.0.0.0 --port 8000 --reload
```

#### 3. Configure ASP.NET Frontend

Open a new terminal:

```bash
cd aspnet_frontend

# Restore NuGet packages
dotnet restore

# Run the application
dotnet run
```

#### 4. Access the Application

Open your browser to: `https://localhost:5001`

## AWS Setup

### 1. Amazon Bedrock

```bash
# Enable Claude 3 Haiku model in AWS Console
# Navigate to: Bedrock → Model access → Request model access
# Select: Claude 3 Haiku
```

### 2. OpenSearch Serverless (Optional - for RAG)

```bash
# Create collection
aws opensearchserverless create-collection \
  --name derivai-isda-rules \
  --type VECTORSEARCH

# Create index (run from Python)
python ingest_isda_rules.py
```

### 3. DynamoDB Audit Table

```bash
aws dynamodb create-table \
  --table-name DerivAI_AuditTrail \
  --attribute-definitions \
    AttributeName=audit_id,AttributeType=S \
    AttributeName=timestamp,AttributeType=S \
  --key-schema \
    AttributeName=audit_id,KeyType=HASH \
    AttributeName=timestamp,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST
```

## 🔧 Configuration

### Environment Variables

**Python Backend (.env):**
```bash
AWS_REGION=us-east-1
BEDROCK_MODEL_ID=anthropic.claude-3-haiku-20240307-v1:0
OPENSEARCH_URL=https://your-endpoint.region.aoss.amazonaws.com
OPENSEARCH_INDEX=isda-rules
AUDIT_TABLE_NAME=DerivAI_AuditTrail
AGENT_API_PORT=8000
```

**ASP.NET Frontend (appsettings.json):**
```json
{
  "AgentSettings": {
    "BaseUrl": "http://localhost:8000"
  }
}
```

## Production Deployment

### Option 1: AWS ECS Fargate 

**Python Backend:**
```bash
# Build Docker image
docker build -t derivai-agent:latest -f Dockerfile.agent .

# Push to ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin <account-id>.dkr.ecr.us-east-1.amazonaws.com
docker tag derivai-agent:latest <account-id>.dkr.ecr.us-east-1.amazonaws.com/derivai-agent:latest
docker push <account-id>.dkr.ecr.us-east-1.amazonaws.com/derivai-agent:latest

# Deploy to ECS
aws ecs create-service \
  --cluster derivai-cluster \
  --service-name derivai-agent \
  --task-definition derivai-agent:1 \
  --desired-count 2 \
  --launch-type FARGATE
```

**ASP.NET Frontend:**
```bash
# Publish application
dotnet publish -c Release -o ./publish

# Deploy to ECS or Elastic Beanstalk
eb init -p "64bit Amazon Linux 2023 v3.0.0 running .NET 8" derivai-web
eb create derivai-web-prod
```

### Option 2: AWS Lambda + API Gateway

**Python Backend:**
```bash
# Package Lambda function
pip install -r requirements.txt -t package/
cd package && zip -r ../lambda.zip . && cd ..
zip -g lambda.zip deriv_ai_agent.py api_bridge.py

# Deploy Lambda
aws lambda create-function \
  --function-name DerivAI-Agent \
  --runtime python3.11 \
  --role arn:aws:iam::<account-id>:role/lambda-execution-role \
  --handler api_bridge.lambda_handler \
  --zip-file fileb://lambda.zip \
  --timeout 120 \
  --memory-size 1024
```

### Option 3: Azure App Service

```bash
# ASP.NET Frontend
az webapp up --name derivai-web --runtime "DOTNETCORE:8.0"

# Python Backend
az webapp up --name derivai-agent --runtime "PYTHON:3.11"
```

## Cost Estimation (Production)

### Monthly Costs (1000 trades/day)

| Service | Usage | Monthly Cost |
|---------|-------|--------------|
| **Amazon Bedrock (Claude 3 Haiku)** | 30K requests × 2K tokens avg | ~$45 |
| **OpenSearch Serverless** | 1 OCU × 730 hours | ~$700 |
| **DynamoDB** | 1M writes, 100K reads | ~$2 |
| **ECS Fargate** | 2 tasks × 0.5 vCPU × 1GB | ~$30 |
| **Application Load Balancer** | 1 ALB | ~$20 |
| **Data Transfer** | 100GB outbound | ~$9 |
| **CloudWatch Logs** | 10GB ingestion | ~$5 |
| **Total** | | **~$811/month** |

### Cost Optimization Tips

1. **Use Claude 3 Haiku** instead of Sonnet (10x cheaper, sufficient for this use case)
2. **Disable OpenSearch** if RAG not needed (~$700 savings)
3. **Use Lambda** instead of ECS for low-volume workloads
4. **Enable DynamoDB TTL** to auto-delete old audit records
5. **Use Reserved Capacity** for predictable workloads (30-50% savings)

### Minimal Setup (No RAG)

| Service | Monthly Cost |
|---------|--------------|
| Bedrock (Haiku) | ~$45 |
| DynamoDB | ~$2 |
| Lambda | ~$5 |
| **Total** | **~$52/month** |

## 📊 Sample Data

The system includes 4 sample trades with intentional mismatches:

- **TRADE-001**: Notional mismatch ($50M vs $50.5M)
- **TRADE-002**: Fixed rate mismatch (4.25% vs 4.35%)
- **TRADE-003**: Day count + payment frequency mismatch
- **TRADE-004**: Perfect match (no breaks)

## 🧪 Testing

```bash
# Python backend tests
cd python_agents
pytest tests/

# ASP.NET frontend tests
cd aspnet_frontend
dotnet test
```

## 📝 API Documentation

### Python Backend Endpoints

**POST /api/analyze-confirmation**
```json
{
  "trade_id": "TRADE-001",
  "our_confirmation": { ... },
  "cpty_confirmation": { ... }
}
```

**GET /api/health**
```json
{
  "status": "ok",
  "model": "claude-haiku",
  "region": "us-east-1"
}
```

## Security Considerations

- AWS credentials managed via IAM roles (no hardcoded keys)
- HTTPS enforced in production
- Input validation on all API endpoints
- Rate limiting on Bedrock API calls
- Audit trail for all AI analysis activities
- No PII stored in logs or audit records

## Contributing

Contributions welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Submit a pull request with tests

## 📄 License

MIT License - see LICENSE file for details

## Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/DerivAI/issues)
- **Documentation**: [Wiki](https://github.com/yourusername/DerivAI/wiki)

## Acknowledgments

- Built with Amazon Bedrock and Claude 3
- LangChain & LangGraph for agent orchestration
- ISDA for derivatives market standards

---

**Note**: This is a demonstration project. For production use in financial services, ensure compliance with:
- MiFID II / Dodd-Frank reporting requirements
- GDPR / data privacy regulations
- Internal risk management policies
- Regulatory approval for AI/ML systems
