public class ScoreService : IScoreService
{
    public int Score { get; private set; }

    public void AddScore(SC_Gem gem)
    {
        if (gem)
        {
            Score += gem.scoreValue;
        }
    }

    public void Reset()
    {
        Score = 0;
    }
}
