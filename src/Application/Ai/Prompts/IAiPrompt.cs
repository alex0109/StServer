using Domain.Utility.Ai;

namespace Application.Ai.Prompts;

// Rules for one AI task: how the request is built and how the answer is checked
public interface IAiPrompt<T>
{
    AiFeature Feature { get; }

    AiRequest BuildRequest();

    // Throws AiOutputException when the answer is not usable
    T Parse(string json);
}
