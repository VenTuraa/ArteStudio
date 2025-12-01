public class ScoreService : IScoreService
{
    public int Score { get; private set; }
    public event System.Action<int> OnScoreChanged;

    public void AddScore(SC_Gem gem)
    {
        if (gem)
        {
            Score += gem.scoreValue;
            OnScoreChanged?.Invoke(Score);
        }
    }

    public void Reset()
    {
        Score = 0;
        OnScoreChanged?.Invoke(Score);
    }
}
