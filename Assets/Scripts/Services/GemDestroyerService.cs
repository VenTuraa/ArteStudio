using System;
using UnityEngine;
using Zenject;

public class GemDestroyerService : IGemDestroyer
{
    private readonly IGameBoard gameBoard;
    private readonly GemPool gemPool;

    [Inject]
    public GemDestroyerService(IGameBoard gameBoard, GemPool gemPool)
    {
        this.gameBoard = gameBoard;
        this.gemPool = gemPool;
    }

    public void DestroyGem(Vector2Int pos, Func<SC_Gem, bool> condition)
    {
        SC_Gem curGem = gameBoard.GetGem(pos.x, pos.y);
        if (curGem && condition(curGem))
        {
            DestroyGemAt(pos);
        }
    }

    public void DestroyGemAt(Vector2Int pos)
    {
        SC_Gem curGem = gameBoard.GetGem(pos.x, pos.y);
        if (!curGem)
            return;

        if (curGem.destroyEffect)
            UnityEngine.Object.Instantiate(curGem.destroyEffect, new Vector2(pos.x, pos.y), Quaternion.identity);

        gameBoard.SetGem(pos.x, pos.y, null);
        if (gameBoard is GameBoard board)
        {
            board.ActiveGems.Remove(curGem);
        }
        gemPool.ReturnToPool(curGem);
    }
}
