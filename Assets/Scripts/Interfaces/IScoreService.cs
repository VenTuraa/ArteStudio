using System;

public interface IScoreService
{
    int Score { get; }
    event Action<int> OnScoreChanged;
    void AddScore(SC_Gem gem);
    void Reset();
}
