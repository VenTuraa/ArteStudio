using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IBombHandler
{
    void CheckBombMatch(int x, int y, SC_Gem bombGem, List<SC_Gem> currentMatches);
    void CheckBombToBombMatch(int x, int y, SC_Gem bombGem, List<SC_Gem> currentMatches);
    GlobalEnums.GemType GetGemColorForMatch(SC_Gem gem);
    List<Vector2Int> GetBombExplosionPattern(Vector2Int bombPos);
    void ApplyBombColor(SC_Gem bomb, GlobalEnums.GemType gemType);
    UniTask HandleBombExplosions(List<SC_Gem> bombs, HashSet<SC_Gem> explodingBombs);
    HashSet<Vector2Int> CreateBombsFromMatches();
    void RemoveNewBombsFromMatches(HashSet<Vector2Int> newBombPositions, List<SC_Gem> currentMatches);
}
