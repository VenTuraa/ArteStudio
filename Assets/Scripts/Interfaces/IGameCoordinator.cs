using UnityEngine;

public interface IGameCoordinator
{
    void SetGem(int x, int y, SC_Gem gem);
    SC_Gem GetGem(int x, int y);
    void SetState(GlobalEnums.GameState state);
    GlobalEnums.GameState CurrentState { get; }
}


