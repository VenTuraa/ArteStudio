public interface IScoreService
{
    int Score { get; }
    void AddScore(SC_Gem gem);
    void Reset();
}
