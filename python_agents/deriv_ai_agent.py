"""
DerivAI - Derivatives Trade Confirmation & Reconciliation AI Agent

LangGraph-based multi-step AI agent that:
  1. Ingests two trade confirmation JSON objects (our bank vs counterparty)
  2. Detects field-level mismatches (breaks)
  3. Calls Amazon Bedrock (Haiku/Claude) to explain each break
  4. Falls back to RAG over ISDA rules stored in OpenSearch Serverless
  5. Returns a structured BreakReport with resolution recommendations
"""

import os
import json
import uuid
import boto3
import logging
from datetime import datetime
from typing import TypedDict, List, Optional, Annotated
from dotenv import load_dotenv

from langchain_aws import ChatBedrock, BedrockEmbeddings
from langchain_core.messages import HumanMessage, SystemMessage, AIMessage
from langchain_core.prompts import ChatPromptTemplate
from langchain_core.output_parsers import StrOutputParser
from langchain_community.vectorstores import OpenSearchVectorSearch
from langgraph.graph import StateGraph, END
from langgraph.graph.message import add_messages

load_dotenv()

AWS_REGION           = os.getenv("AWS_REGION", "us-east-1")
BEDROCK_MODEL_ID     = os.getenv("BEDROCK_MODEL_ID", "anthropic.claude-3-haiku-20240307-v1:0")
OPENSEARCH_URL       = os.getenv("OPENSEARCH_URL", "")
OPENSEARCH_INDEX     = os.getenv("OPENSEARCH_INDEX", "isda-rules")
AUDIT_TABLE_NAME     = os.getenv("AUDIT_TABLE_NAME", "DerivAI_AuditTrail")

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("DerivAI")

bedrock_runtime = boto3.client("bedrock-runtime", region_name=AWS_REGION)
dynamodb        = boto3.resource("dynamodb",       region_name=AWS_REGION)

from opensearchpy import RequestsHttpConnection

llm = ChatBedrock(
    model_id=BEDROCK_MODEL_ID,
    region_name=AWS_REGION,
    model_kwargs={"temperature": 0.2, "max_tokens": 2048},
)

embeddings = BedrockEmbeddings(
    model_id="amazon.titan-embed-text-v2:0",
    region_name=AWS_REGION,
)


def get_vector_store():
    """
    Initialize OpenSearch Serverless vector store with AWS SigV4 authentication.
    Used for RAG retrieval of ISDA rules during break analysis.
    """
    import boto3
    import requests
    from requests_aws4auth import AWS4Auth

    credentials = boto3.Session().get_credentials().get_frozen_credentials()
    auth        = AWS4Auth(
        credentials.access_key,
        credentials.secret_key,
        AWS_REGION,
        "aoss",
        session_token=credentials.token,
    )

    from langchain_community.vectorstores import OpenSearchVectorSearch
    from opensearchpy import RequestsHttpConnection

    return OpenSearchVectorSearch(
        opensearch_url   = OPENSEARCH_URL,
        index_name       = OPENSEARCH_INDEX,
        embedding_function = embeddings,
        http_auth        = auth,
        use_ssl          = True,
        verify_certs     = True,
        connection_class = RequestsHttpConnection,
        is_aoss          = True,
    )


class TradeReconciliationState(TypedDict):
    """
    LangGraph state that flows through every node.
    
    Fields:
    - our_trade: Internal trade confirmation data
    - cpty_trade: Counterparty trade confirmation data
    - breaks: List of field-level mismatches detected
    - rag_context: Relevant ISDA rule snippets from OpenSearch
    - llm_explanation: Natural-language break explanation from Claude
    - resolution: Recommended resolution action
    - audit_id: Unique ID written to DynamoDB audit trail
    - messages: LangGraph message history
    """
    our_trade       : dict
    cpty_trade      : dict
    breaks          : List[dict]
    rag_context     : str
    llm_explanation : str
    resolution      : str
    audit_id        : str
    messages        : Annotated[list, add_messages]


def detect_breaks(state: TradeReconciliationState) -> TradeReconciliationState:
    """
    NODE 1: Deterministic field-by-field comparison between our trade and counterparty trade.
    Any field with a differing value is recorded as a 'break' with conflicting values preserved.
    No LLM call - pure Python logic for fast, deterministic, auditable break detection.
    """
    logger.info("[Node 1] Detecting breaks between our trade and counterparty trade")

    our   = state["our_trade"]
    cpty  = state["cpty_trade"]
    breaks = []

    RECONCILIATION_FIELDS = [
        "trade_id", "instrument_type", "notional_amount", "notional_currency",
        "trade_date", "effective_date", "termination_date", "fixed_rate",
        "floating_rate_index", "payment_frequency", "day_count_convention",
        "business_day_convention", "collateral_type", "governing_law",
        "clearing_house", "settlement_currency",
    ]

    for field in RECONCILIATION_FIELDS:
        our_val  = our.get(field)
        cpty_val = cpty.get(field)

        if isinstance(our_val, str) and isinstance(cpty_val, str):
            if our_val.strip().lower() != cpty_val.strip().lower():
                breaks.append({
                    "field"       : field,
                    "our_value"   : our_val,
                    "cpty_value"  : cpty_val,
                    "severity"    : _classify_severity(field),
                })
        elif our_val != cpty_val:
            breaks.append({
                "field"      : field,
                "our_value"  : str(our_val),
                "cpty_value" : str(cpty_val),
                "severity"   : _classify_severity(field),
            })

    logger.info(f"[Node 1] Detected {len(breaks)} break(s)")
    return {**state, "breaks": breaks}


def _classify_severity(field: str) -> str:
    """
    Classify break severity based on business impact.
    HIGH = material economic/legal impact
    MEDIUM = operational/settlement impact
    LOW = administrative/reference data
    """
    HIGH_FIELDS   = {"notional_amount", "fixed_rate", "floating_rate_index",
                     "termination_date", "governing_law", "clearing_house"}
    MEDIUM_FIELDS = {"payment_frequency", "day_count_convention",
                     "effective_date", "settlement_currency", "collateral_type"}
    if field in HIGH_FIELDS:
        return "HIGH"
    if field in MEDIUM_FIELDS:
        return "MEDIUM"
    return "LOW"


def retrieve_isda_context(state: TradeReconciliationState) -> TradeReconciliationState:
    """
    NODE 2: RAG node - retrieves relevant ISDA/CSA rule snippets from OpenSearch Serverless
    using semantic search. Retrieved passages are injected into the LLM prompt in the next node.
    """
    if not state["breaks"]:
        logger.info("[Node 2] No breaks — skipping RAG retrieval")
        return {**state, "rag_context": ""}

    logger.info("[Node 2] Retrieving ISDA context from OpenSearch for breaks")

    try:
        vector_store = get_vector_store()
        context_parts = []

        for brk in state["breaks"]:
            query = (
                f"ISDA rules for {brk['field']} discrepancy in derivatives trade confirmation. "
                f"Our value: {brk['our_value']}. Counterparty value: {brk['cpty_value']}."
            )
            docs = vector_store.similarity_search(query, k=3)
            for doc in docs:
                context_parts.append(
                    f"[ISDA Rule - {brk['field']}]\n{doc.page_content}\n"
                )

        rag_context = "\n\n".join(context_parts)
        logger.info(f"[Node 2] Retrieved {len(context_parts)} ISDA rule chunks")
        return {**state, "rag_context": rag_context}

    except Exception as exc:
        logger.warning(f"[Node 2] RAG retrieval failed (continuing without context): {exc}")
        return {**state, "rag_context": "ISDA rule retrieval unavailable."}


def explain_breaks_with_llm(state: TradeReconciliationState) -> TradeReconciliationState:
    """
    NODE 3: Core LLM node - calls Amazon Bedrock (Claude) with:
    - Full trade details for both sides
    - Detected breaks
    - RAG-retrieved ISDA rule context
    
    Returns:
    1. Plain-English explanation of each break
    2. Regulatory/market convention reason the break matters
    3. Recommended resolution action
    """
    if not state["breaks"]:
        logger.info("[Node 3] No breaks — skipping LLM explanation")
        return {**state, "llm_explanation": "No breaks detected. Trades match.", "resolution": "MATCHED"}

    logger.info("[Node 3] Calling Amazon Bedrock Claude for break explanation")

    system_prompt = """You are DerivAI, an expert derivatives middle-office AI assistant with deep
knowledge of ISDA Master Agreements, Credit Support Annexes (CSA), and global
derivatives trade confirmation standards (FpML, SWIFT MT300/MT320).

Your task is to:
1. Analyse the field-level discrepancies between the two trade confirmations.
2. Explain each break clearly in plain English, citing relevant ISDA rules when available.
3. Assess the business / regulatory risk each break poses.
4. Recommend the most appropriate resolution action.

Always be precise and professional. Reference specific ISDA sections when cited in context.
Format your response as structured JSON."""

    user_message = f"""
=== OUR TRADE CONFIRMATION ===
{json.dumps(state['our_trade'], indent=2)}

=== COUNTERPARTY TRADE CONFIRMATION ===
{json.dumps(state['cpty_trade'], indent=2)}

=== DETECTED BREAKS ===
{json.dumps(state['breaks'], indent=2)}

=== RELEVANT ISDA RULES (retrieved via RAG) ===
{state['rag_context'] or 'No ISDA context retrieved.'}

Please provide your analysis in the following JSON format:
{{
  "summary": "<one-sentence summary of the overall match status>",
  "break_explanations": [
    {{
      "field": "<field name>",
      "severity": "<HIGH|MEDIUM|LOW>",
      "explanation": "<plain-English explanation of why this break occurred>",
      "isda_reference": "<ISDA section or rule if applicable>",
      "recommended_action": "<what operations team should do>"
    }}
  ],
  "overall_resolution": "<CANCEL_AND_REBOOK | AMEND_OURS | AMEND_COUNTERPARTY | ESCALATE | MATCHED>",
  "resolution_rationale": "<reason for the recommended overall resolution>"
}}
"""

    prompt = ChatPromptTemplate.from_messages([
        SystemMessage(content=system_prompt),
        HumanMessage(content=user_message),
    ])

    chain  = prompt | llm | StrOutputParser()
    result = chain.invoke({})

    result = result.replace("```json", "").replace("```", "").strip()

    try:
        parsed = json.loads(result)
        resolution = parsed.get("overall_resolution", "UNKNOWN")
    except json.JSONDecodeError:
        parsed     = {"raw_response": result}
        resolution = "UNKNOWN"

    logger.info(f"[Node 3] LLM resolution recommendation: {resolution}")
    return {**state, "llm_explanation": json.dumps(parsed, indent=2), "resolution": resolution}


def write_audit_trail(state: TradeReconciliationState) -> TradeReconciliationState:
    """
    NODE 4: Persists the full reconciliation result to Amazon DynamoDB for compliance
    and audit purposes. Every reconciliation run (matched or broken) is recorded with
    UUID, timestamp, trade IDs, and full LLM output.
    """
    audit_id  = str(uuid.uuid4())
    timestamp = datetime.utcnow().isoformat() + "Z"

    item = {
        "audit_id"        : audit_id,
        "timestamp"       : timestamp,
        "our_trade_id"    : state["our_trade"].get("trade_id", "UNKNOWN"),
        "cpty_trade_id"   : state["cpty_trade"].get("trade_id", "UNKNOWN"),
        "break_count"     : len(state["breaks"]),
        "resolution"      : state["resolution"],
        "llm_explanation" : state["llm_explanation"],
        "breaks_detail"   : json.dumps(state["breaks"]),
    }

    try:
        table = dynamodb.Table(AUDIT_TABLE_NAME)
        table.put_item(Item=item)
        logger.info(f"[Node 4] Audit record written: {audit_id}")
    except Exception as exc:
        logger.warning(f"[Node 4] DynamoDB write failed (audit_id={audit_id}): {exc}")

    return {**state, "audit_id": audit_id}


def route_after_break_detection(state: TradeReconciliationState) -> str:
    """
    Conditional edge: if no breaks found, skip directly to audit.
    Otherwise proceed through RAG then LLM.
    """
    if not state["breaks"]:
        return "write_audit"
    return "retrieve_isda_context"


def build_graph() -> StateGraph:
    """
    Constructs the LangGraph workflow:
    detect_breaks → retrieve_isda_context → explain_breaks_with_llm → write_audit_trail → END
    """
    graph = StateGraph(TradeReconciliationState)

    graph.add_node("detect_breaks",           detect_breaks)
    graph.add_node("retrieve_isda_context",   retrieve_isda_context)
    graph.add_node("explain_breaks_with_llm", explain_breaks_with_llm)
    graph.add_node("write_audit_trail",       write_audit_trail)

    graph.set_entry_point("detect_breaks")

    graph.add_conditional_edges(
        "detect_breaks",
        route_after_break_detection,
        {
            "retrieve_isda_context" : "retrieve_isda_context",
            "write_audit"           : "write_audit_trail",
        },
    )

    graph.add_edge("retrieve_isda_context",   "explain_breaks_with_llm")
    graph.add_edge("explain_breaks_with_llm", "write_audit_trail")
    graph.add_edge("write_audit_trail",       END)

    return graph.compile()


def run_reconciliation(our_trade: dict, cpty_trade: dict) -> dict:
    """
    Entry point for the reconciliation workflow.
    
    Parameters:
    - our_trade: Internal trade confirmation fields
    - cpty_trade: Counterparty-received trade confirmation fields
    
    Returns:
    - dict with keys: breaks, resolution, llm_explanation, audit_id
    """
    app = build_graph()

    initial_state: TradeReconciliationState = {
        "our_trade"       : our_trade,
        "cpty_trade"      : cpty_trade,
        "breaks"          : [],
        "rag_context"     : "",
        "llm_explanation" : "",
        "resolution"      : "",
        "audit_id"        : "",
        "messages"        : [],
    }

    final_state = app.invoke(initial_state)

    return {
        "audit_id"        : final_state["audit_id"],
        "break_count"     : len(final_state["breaks"]),
        "breaks"          : final_state["breaks"],
        "resolution"      : final_state["resolution"],
        "llm_explanation" : final_state["llm_explanation"],
    }


def lambda_handler(event, context):
    """
    AWS Lambda entry point.
    Expects event body: { "our_trade": {...}, "cpty_trade": {...} }
    """
    try:
        body       = json.loads(event.get("body", "{}"))
        our_trade  = body["our_trade"]
        cpty_trade = body["cpty_trade"]
        result     = run_reconciliation(our_trade, cpty_trade)

        return {
            "statusCode" : 200,
            "headers"    : {"Content-Type": "application/json",
                            "Access-Control-Allow-Origin": "*"},
            "body"       : json.dumps(result),
        }
    except Exception as exc:
        logger.error(f"Lambda handler error: {exc}")
        return {
            "statusCode" : 500,
            "body"       : json.dumps({"error": str(exc)}),
        }


if __name__ == "__main__":
    with open("../sample_data/our_trade.json")   as f: our_trade  = json.load(f)
    with open("../sample_data/cpty_trade.json")  as f: cpty_trade = json.load(f)

    result = run_reconciliation(our_trade, cpty_trade)
    print(json.dumps(result, indent=2))
