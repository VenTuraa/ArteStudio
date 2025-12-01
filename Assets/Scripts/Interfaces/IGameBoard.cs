using System.Collections.Generic;
using UnityEngine;

public interface IGameBoard
{
    int Width { get; }
    int Height { get; }
    List<SC_Gem> CurrentMatches { get; }
    HashSet<SC_Gem> ActiveGems { get; }
    List<GameBoard.BombCreationInfo> BombsToCreate { get; }
    
    void SetGem(int x, int y, SC_Gem gem);
    SC_Gem GetGem(int x, int y);
    bool MatchesAt(Vector2Int position, SC_Gem gemToCheck);
    void FindAllMatches();
    void SetBombHandler(IBombHandler handler);
}
