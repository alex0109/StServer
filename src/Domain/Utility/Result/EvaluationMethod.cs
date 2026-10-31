namespace Domain.Utility.Result;

public enum EvaluationMethod
{
    None,
    Exact,
    Fuzzy,
    Embedding,
    AI,
    // The user accepted the answer after automatic checking said no; new values only at the end
    Manual
}
