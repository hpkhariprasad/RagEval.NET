namespace RagEval.Prompts;

/// <summary>
/// All prompt text sent to the judge LLM. Every user-prompt template instructs the model
/// to respond with valid JSON only, matching an explicit schema, so responses can be
/// parsed deterministically without regular expressions.
/// </summary>
public static class MetricPrompts
{
    /// <summary>Shared system prompt establishing the judge's role and response format.</summary>
    public const string JsonOnlySystemPrompt =
        "You are an impartial evaluator of retrieval-augmented generation (RAG) pipeline output. " +
        "You must respond with valid JSON only. Do not include markdown code fences, explanations, " +
        "or any text outside of the single JSON object requested by the user prompt.";

    /// <summary>Separator used to join multiple context chunks into a single block of text for a prompt.</summary>
    public const string ContextSeparator = "\n\n---\n\n";

    /// <summary>Extracts the factual claims made in an answer, used by the faithfulness metric.</summary>
    public const string FaithfulnessClaimExtraction = """
        Extract all distinct factual claims made in the following answer. Break the answer down
        into the smallest independently verifiable factual statements. Ignore opinions, greetings,
        or filler text that makes no factual assertion.

        Answer:
        "{0}"

        Respond with valid JSON only, matching exactly this schema:
        {{
          "claims": ["claim 1", "claim 2"]
        }}

        If the answer contains no factual claims, return an empty array.
        """;

    /// <summary>Verifies whether a single claim is supported by the retrieved context, used by the faithfulness metric.</summary>
    public const string FaithfulnessClaimVerification = """
        Determine whether the following claim is fully supported by the given context. A claim is
        supported only if the context directly states it or it can be directly inferred from the
        context without additional assumptions.

        Claim:
        "{0}"

        Context:
        "{1}"

        Respond with valid JSON only, matching exactly this schema:
        {{
          "supported": true,
          "reason": "brief explanation"
        }}
        """;

    /// <summary>Generates reverse-engineered questions for an answer, used by the answer relevance metric.</summary>
    public const string AnswerRelevanceReverseQuestion = """
        Given the following answer, generate exactly 3 questions that this answer would be a good,
        complete response to. The questions should be diverse in phrasing, but each must
        independently be answerable using only the given answer.

        Answer:
        "{0}"

        Respond with valid JSON only, matching exactly this schema:
        {{
          "questions": ["question 1", "question 2", "question 3"]
        }}
        """;

    /// <summary>Scores the similarity between the original question and a reverse-engineered question, used by the answer relevance metric.</summary>
    public const string AnswerRelevanceSimilarityScore = """
        Score how semantically similar the following two questions are, on a continuous scale from
        0.0 (completely unrelated) to 1.0 (identical meaning).

        Original question:
        "{0}"

        Generated question:
        "{1}"

        Respond with valid JSON only, matching exactly this schema:
        {{
          "similarity": 0.0
        }}
        """;

    /// <summary>Determines whether a single context chunk was useful in answering the question, used by the context precision metric.</summary>
    public const string ContextPrecisionChunkUsefulness = """
        Determine whether the following context chunk contributed useful information toward
        answering the question. A chunk is useful only if it contains information that was
        actually needed to answer the question, not merely topically related.

        Question:
        "{0}"

        Context chunk:
        "{1}"

        Respond with valid JSON only, matching exactly this schema:
        {{
          "useful": true,
          "reason": "brief explanation"
        }}
        """;

    /// <summary>Extracts the factual claims made in a ground-truth answer, used by the context recall metric.</summary>
    public const string ContextRecallClaimExtraction = """
        Extract all distinct factual claims made in the following ground truth answer. Break it
        down into the smallest independently verifiable factual statements.

        Ground truth:
        "{0}"

        Respond with valid JSON only, matching exactly this schema:
        {{
          "claims": ["claim 1", "claim 2"]
        }}

        If the ground truth contains no factual claims, return an empty array.
        """;

    /// <summary>Determines whether a ground-truth claim is covered by the retrieved context, used by the context recall metric.</summary>
    public const string ContextRecallClaimCoverage = """
        Determine whether the following claim from a ground truth answer is present in, or can be
        directly inferred from, the given context.

        Claim:
        "{0}"

        Context:
        "{1}"

        Respond with valid JSON only, matching exactly this schema:
        {{
          "covered": true,
          "reason": "brief explanation"
        }}
        """;
}
