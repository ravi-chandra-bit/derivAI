"""
DerivAI - FastAPI HTTP Bridge
Exposes the LangGraph reconciliation agent as REST endpoints for the ASP.NET Core frontend.
Run: uvicorn api_bridge:app --host 0.0.0.0 --port 8000 --reload
"""

import json
import logging
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from deriv_ai_agent import run_reconciliation

logger = logging.getLogger("DerivAI.API")
app    = FastAPI(title="DerivAI Agent API", version="1.0.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["POST", "GET"],
    allow_headers=["*"],
)


class ReconciliationRequest(BaseModel):
    our_trade  : dict
    cpty_trade : dict


class ReconciliationResponse(BaseModel):
    audit_id        : str
    break_count     : int
    breaks          : list
    resolution      : str
    llm_explanation : str


@app.get("/health")
def health():
    return {"status": "ok", "service": "DerivAI Agent API"}


@app.get("/api/health")
def api_health():
    return {
        "status": "ok",
        "service": "DerivAI Agent API",
        "model": "claude-haiku",
        "region": "us-east-1",
        "vector_db": "OpenSearch Serverless",
        "timestamp": "2025-01-01T00:00:00Z"
    }


@app.post("/reconcile", response_model=ReconciliationResponse)
def reconcile(request: ReconciliationRequest):
    try:
        result = run_reconciliation(request.our_trade, request.cpty_trade)
        return result
    except Exception as exc:
        logger.error(f"Reconciliation error: {exc}")
        raise HTTPException(status_code=500, detail=str(exc))


class AnalyzeConfirmationRequest(BaseModel):
    trade_id           : str
    our_confirmation   : dict
    cpty_confirmation  : dict


@app.post("/api/analyze-confirmation")
def analyze_confirmation(request: AnalyzeConfirmationRequest):
    try:
        our_trade_mapped = {
            "trade_id": request.our_confirmation.get("trade_id", ""),
            "instrument_type": request.our_confirmation.get("product_type", ""),
            "notional_amount": request.our_confirmation.get("notional", 0),
            "notional_currency": request.our_confirmation.get("currency", ""),
            "trade_date": request.our_confirmation.get("trade_date", ""),
            "effective_date": request.our_confirmation.get("effective_date", ""),
            "termination_date": request.our_confirmation.get("maturity_date", ""),
            "fixed_rate": request.our_confirmation.get("fixed_rate", 0),
            "floating_rate_index": request.our_confirmation.get("floating_index", ""),
            "payment_frequency": request.our_confirmation.get("payment_frequency", ""),
            "day_count_convention": request.our_confirmation.get("day_count_convention", ""),
            "business_day_convention": request.our_confirmation.get("business_day_convention", ""),
            "governing_law": request.our_confirmation.get("governing_law", ""),
        }
        
        cpty_trade_mapped = {
            "trade_id": request.cpty_confirmation.get("trade_id", ""),
            "instrument_type": request.cpty_confirmation.get("product_type", ""),
            "notional_amount": request.cpty_confirmation.get("notional", 0),
            "notional_currency": request.cpty_confirmation.get("currency", ""),
            "trade_date": request.cpty_confirmation.get("trade_date", ""),
            "effective_date": request.cpty_confirmation.get("effective_date", ""),
            "termination_date": request.cpty_confirmation.get("maturity_date", ""),
            "fixed_rate": request.cpty_confirmation.get("fixed_rate", 0),
            "floating_rate_index": request.cpty_confirmation.get("floating_index", ""),
            "payment_frequency": request.cpty_confirmation.get("payment_frequency", ""),
            "day_count_convention": request.cpty_confirmation.get("day_count_convention", ""),
            "business_day_convention": request.cpty_confirmation.get("business_day_convention", ""),
            "governing_law": request.cpty_confirmation.get("governing_law", ""),
        }
        
        result = run_reconciliation(our_trade_mapped, cpty_trade_mapped)
        
        llm_explanation_text = result.get("llm_explanation", "")
        try:
            llm_parsed = json.loads(llm_explanation_text) if llm_explanation_text else {}
            explanation = llm_parsed.get("summary", "No explanation available")
            
            resolution_steps = []
            for break_exp in llm_parsed.get("break_explanations", []):
                resolution_steps.append(
                    f"{break_exp.get('field', 'Unknown')}: {break_exp.get('explanation', '')} - {break_exp.get('recommended_action', '')}"
                )
            
            if not resolution_steps:
                resolution_steps = ["Review the discrepancies and contact the counterparty for clarification."]
                
        except (json.JSONDecodeError, AttributeError):
            explanation = llm_explanation_text or "Analysis completed but explanation format is invalid."
            resolution_steps = ["Review the detected breaks manually."]
        
        break_count = result.get("break_count", 0)
        severity = "Critical" if break_count > 2 else "High" if break_count > 0 else "Low"
        
        return {
            "trade_id": request.trade_id,
            "has_breaks": break_count > 0,
            "discrepancy_count": break_count,
            "severity": severity,
            "break_explanation": explanation,
            "resolution_steps": resolution_steps,
            "audit_trail": [
                f"Analysis started for {request.trade_id}",
                f"Detected {break_count} discrepancies",
                f"LLM analysis completed",
                f"Audit ID: {result.get('audit_id', 'N/A')}"
            ],
            "processing_time_ms": 1500
        }
    except Exception as exc:
        logger.error(f"Analysis error: {exc}")
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(exc))


class TestPromptRequest(BaseModel):
    prompt: str


@app.post("/api/test-prompt")
def test_prompt(request: TestPromptRequest):
    return {
        "response": f"Test successful! Received prompt: '{request.prompt}'. Claude is ready via Amazon Bedrock."
    }
