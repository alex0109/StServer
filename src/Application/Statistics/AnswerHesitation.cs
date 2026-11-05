namespace Application.Statistics;

// Changing a choice before settling on it is a sign of doubt, however sure the user said they were
public static class AnswerHesitation
{
    public const int MaxLevel = 3;

    // 0 none, 1 slight (one change), 2 clear, 3 strong (three changes or more)
    public static int Level(int answerChanges) => Math.Clamp(answerChanges, 0, MaxLevel);
}
