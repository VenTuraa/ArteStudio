using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public interface ICascadeService
{
    UniTask ProcessCascade();
    HashSet<SC_Gem> GetCascadeGems();
    HashSet<Vector2Int> GetCascadePositions();
    void SetGameCoordinator(IGameCoordinator coordinator);
    void SetMatchHandler(IMatchHandlerService matchHandler);
}
