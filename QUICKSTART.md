# DerivAI Quick Start Guide

Get DerivAI running locally in under 10 minutes.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Python 3.11+](https://www.python.org/downloads/)
- [AWS CLI](https://aws.amazon.com/cli/) configured with credentials
- AWS Bedrock access (Claude 3 Haiku model enabled)

## Step 1: Clone Repository

```bash
git clone https://github.com/yourusername/DerivAI.git
cd DerivAI
```

## Step 2: Setup Python Backend (5 minutes)

```bash
cd python_agents

# Create virtual environment
python -m venv venv

# Activate (Windows)
venv\Scripts\activate
# Activate (macOS/Linux)
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt

# Configure environment
cp .env.example .env
# Edit .env and set:
# - AWS_REGION (e.g., us-east-1)
# - BEDROCK_MODEL_ID=anthropic.claude-3-haiku-20240307-v1:0

# Start server
uvicorn api_bridge:app --reload
```

Server running at: http://localhost:8000

## Step 3: Setup ASP.NET Frontend (3 minutes)

Open a new terminal:

```bash
cd aspnet_frontend

# Restore packages
dotnet restore

# Run application
dotnet run
```

Application running at: https://localhost:5001

## Step 4: Test the Application

1. Open browser to https://localhost:5001
2. You'll see the dashboard with 4 sample trades
3. Click "View" on TRADE-001 (has a notional mismatch)
4. Click "Run AI Analysis"
5. Claude will explain the $500K notional discrepancy

## Troubleshooting

### Python Backend Won't Start

**Error**: `ModuleNotFoundError: No module named 'fastapi'`
**Fix**: Ensure virtual environment is activated and dependencies installed

```bash
pip install -r requirements.txt
```

### AWS Credentials Error

**Error**: `Unable to locate credentials`
**Fix**: Configure AWS CLI

```bash
aws configure
# Enter your AWS Access Key ID
# Enter your AWS Secret Access Key
# Enter region (e.g., us-east-1)
```

### Bedrock Access Denied

**Error**: `AccessDeniedException: User is not authorized to perform: bedrock:InvokeModel`
**Fix**: Enable Claude 3 Haiku in AWS Console

1. Go to AWS Bedrock console
2. Click "Model access"
3. Click "Request model access"
4. Select "Claude 3 Haiku"
5. Submit request (usually instant approval)

### Frontend Can't Connect to Backend

**Error**: "Could not connect to the AI agent"
**Fix**: Ensure Python backend is running on port 8000

```bash
# Check if port 8000 is in use
netstat -an | findstr 8000  # Windows
lsof -i :8000               # macOS/Linux
```

## Optional: Setup OpenSearch (RAG)

Skip this for initial testing - the system works without RAG.

```bash
# Create OpenSearch Serverless collection
aws opensearchserverless create-collection \
  --name derivai-isda-rules \
  --type VECTORSEARCH

# Update .env with collection endpoint
OPENSEARCH_URL=https://your-collection-id.us-east-1.aoss.amazonaws.com
```

## Optional: Setup DynamoDB (Audit Trail)

Skip this for initial testing - audit trail will log to console.

```bash
# Create DynamoDB table
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

## Next Steps

- Read [README.md](README.md) for full documentation
- Review [ARCHITECTURE.md](ARCHITECTURE.md) for technical details
- Check [CONTRIBUTING.md](CONTRIBUTING.md) to contribute

## Sample Trades

The system includes 4 pre-loaded trades:

| Trade ID | Issue | Severity |
|----------|-------|----------|
| TRADE-001 | Notional mismatch ($50M vs $50.5M) | Critical |
| TRADE-002 | Fixed rate mismatch (4.25% vs 4.35%) | Critical |
| TRADE-003 | Day count + payment frequency mismatch | High |
| TRADE-004 | Perfect match | None |

## Cost Warning

Running this locally with AWS Bedrock will incur charges:
- ~$0.0003 per analysis (Claude 3 Haiku)
- ~$0.01 for 30 test analyses

Total cost for testing: **< $1**

## Support

- Issues: [GitHub Issues](https://github.com/yourusername/DerivAI/issues)
- Discussions: [GitHub Discussions](https://github.com/yourusername/DerivAI/discussions)

---

**Ready to deploy to production?** See [README.md](README.md#-production-deployment) for deployment options.
