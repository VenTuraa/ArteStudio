using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

public class CascadeService : ICascadeService
{
    private const float FILLED_BOARD_DELAY = 0.5f;
    private const float STAGGER_THRESHOLD = 0.02f;
    private const float MAX_WAIT_TIME = 1.0f;
    private const float CASCADE_DELAY_MULTIPLIER = 0.5f;

    private readonly IGameBoard gameBoard;
    private readonly SC_GameVariables gameVariables;
    private readonly IGemSpawnerService gemSpawner;
    private readonly IGameStateService gameStateService;
    private IMatchHandlerService matchHandler;
    private SC_GameLogic gameLogic;

    private HashSet<SC_Gem> cascadeGems = new();
    private HashSet<Vector2Int> cascadePositions = new();

    public void SetMatchHandler(IMatchHandlerService matchHandler)
    {
        this.matchHandler = matchHandler ?? throw new System.ArgumentNullException(nameof(matchHandler));
    }

    [Inject]
    public CascadeService(
        IGameBoard gameBoard,
        SC_GameVariables gameVariables,
        IGemSpawnerService gemSpawner,
        IGameStateService gameStateService)
    {
        this.gameBoard = gameBoard;
        this.gameVariables = gameVariables;
        this.gemSpawner = gemSpawner;
        this.gameStateService = gameStateService;
    }

    public void SetGameLogic(SC_GameLogic logic)
    {
        gameLogic = logic;
    }

    public async UniTask ProcessCascade()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(0.2f));

        cascadeGems.Clear();
        cascadePositions.Clear();

        for (int x = 0; x < gameBoard.Width; x++)
            await CascadeColumn(x);

        await FilledBoardCo();
    }

    private async UniTask CascadeColumn(int column)
    {
        List<GemDropInfo> dropQueue = BuildDropQueueForColumn(column);
        SpawnNewGemsForColumn(column, dropQueue);

        await ExecuteStaggeredDrop(column, dropQueue);

        foreach (var dropInfo in dropQueue)
        {
            if (dropInfo.gem)
            {
                SC_Gem gemOnBoard = gameBoard.GetGem(column, dropInfo.targetY);
                if (gemOnBoard)
                {
                    Vector2Int pos = new Vector2Int(column, dropInfo.targetY);
                    cascadeGems.Add(gemOnBoard);
                    cascadePositions.Add(pos);
                    
                    if (!gemOnBoard.posIndex.Equals(pos))
                    {
                        gemOnBoard.posIndex = pos;
                    }
                }
                else
                {
                    Vector2Int pos = new Vector2Int(column, dropInfo.targetY);
                    cascadeGems.Add(dropInfo.gem);
                    cascadePositions.Add(pos);
                    dropInfo.gem.posIndex = pos;
                }
            }
        }
    }

    private List<GemDropInfo> BuildDropQueueForColumn(int column)
    {
        List<GemDropInfo> dropQueue = new List<GemDropInfo>();
        int nullCounter = 0;

        for (int y = 0; y < gameBoard.Height; y++)
        {
            SC_Gem currentGem = gameBoard.GetGem(column, y);
            if (!currentGem)
            {
                nullCounter++;
            }
            else if (nullCounter > 0)
            {
                int targetY = y - nullCounter;
                dropQueue.Add(new GemDropInfo
                {
                    gem = currentGem,
                    sourceY = y,
                    targetY = targetY
                });
            }
        }

        return dropQueue;
    }

    private void SpawnNewGemsForColumn(int column, List<GemDropInfo> dropQueue)
    {
        int nullCounter = CountNullsInColumn(column);
        List<GemDropInfo> newGemsQueue = new List<GemDropInfo>();

        for (int i = 0; i < nullCounter; i++)
        {
            int targetY = gameBoard.Height - nullCounter + i;
            int spawnY = gameBoard.Height + i;

            SC_Gem safeGem = gemSpawner.GetSafeGemForPosition(column, targetY, dropQueue, newGemsQueue);
            SC_Gem newGem = gemSpawner.SpawnGemAtPosition(column, spawnY, safeGem);

            if (newGem)
            {
                if (newGem.type != GlobalEnums.GemType.bomb)
                {
                    newGem.GemColor = newGem.type;
                }

                if (gameLogic)
                {
                    newGem.SetupGem(gameLogic, new Vector2Int(column, spawnY));
                }

                GemDropInfo newGemInfo = new GemDropInfo
                {
                    gem = newGem,
                    sourceY = spawnY,
                    targetY = targetY
                };

                newGemsQueue.Add(newGemInfo);
                dropQueue.Add(newGemInfo);
            }
        }

        dropQueue.Sort((a, b) => a.targetY.CompareTo(b.targetY));
    }

    private int CountNullsInColumn(int column)
    {
        int count = 0;
        for (int y = 0; y < gameBoard.Height; y++)
        {
            if (!gameBoard.GetGem(column, y))
                count++;
        }

        return count;
    }

    private async UniTask ExecuteStaggeredDrop(int column, List<GemDropInfo> dropQueue)
    {
        for (int i = 0; i < dropQueue.Count; i++)
        {
            GemDropInfo dropInfo = dropQueue[i];
            dropInfo.gem.posIndex = new Vector2Int(column, dropInfo.targetY);
            gameBoard.SetGem(column, dropInfo.targetY, dropInfo.gem);

            if (dropInfo.sourceY < gameBoard.Height)
            {
                gameBoard.SetGem(column, dropInfo.sourceY, null);
            }

            if (i < dropQueue.Count - 1)
            {
                await WaitForGemChainTrigger(dropInfo.gem, dropInfo.sourceY, STAGGER_THRESHOLD);
            }
        }
    }

    private async UniTask WaitForGemChainTrigger(SC_Gem gem, float startY, float threshold)
    {
        if (!gem)
            return;

        float startTime = Time.time;

        while (Time.time - startTime < MAX_WAIT_TIME)
        {
            if (!gem)
                break;

            float currentY = gem.transform.position.y;
            float distanceMoved = startY - currentY;

            if (distanceMoved >= threshold)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(gameVariables.cascadeDelay * CASCADE_DELAY_MULTIPLIER));
                return;
            }

            await UniTask.Yield();
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(gameVariables.cascadeDelay));
    }

    private async UniTask FilledBoardCo()
    {
        await DelaySeconds(FILLED_BOARD_DELAY);

        CheckMisplacedGems();
        await DelaySeconds(FILLED_BOARD_DELAY);

        UpdateAllGemPositions();
        gameBoard.FindAllMatches();

        if (cascadeGems.Count > 0 || cascadePositions.Count > 0)
        {
            if (matchHandler == null)
                return;

            List<SC_Gem> validCascadeMatches = matchHandler.FilterValidCascadeMatches(gameBoard.CurrentMatches);

            if (validCascadeMatches.Count > 0)
            {
                matchHandler.ProcessCascadeMatches(validCascadeMatches);
                await DelaySeconds(FILLED_BOARD_DELAY);
                matchHandler.DestroyMatches();
            }
            else
            {
                ClearMatchesAndReturnToMoveState();
                await DelaySeconds(FILLED_BOARD_DELAY);
            }
        }
        else
        {
            ClearMatchesAndReturnToMoveState();
            await DelaySeconds(FILLED_BOARD_DELAY);
        }

        cascadeGems.Clear();
        cascadePositions.Clear();
    }

    private void UpdateAllGemPositions()
    {
        for (int x = 0; x < gameBoard.Width; x++)
        {
            for (int y = 0; y < gameBoard.Height; y++)
            {
                SC_Gem gem = gameBoard.GetGem(x, y);
                if (gem && gem.posIndex != new Vector2Int(x, y))
                {
                    gem.posIndex = new Vector2Int(x, y);
                }
            }
        }
    }

    private void CheckMisplacedGems()
    {
        HashSet<SC_Gem> foundGems = new HashSet<SC_Gem>(gameBoard.ActiveGems);

        for (int x = 0; x < gameBoard.Width; x++)
        {
            for (int y = 0; y < gameBoard.Height; y++)
            {
                SC_Gem curGem = gameBoard.GetGem(x, y);
                if (curGem)
                    foundGems.Remove(curGem);
            }
        }
    }

    private void ClearMatchesAndReturnToMoveState()
    {
        gameBoard.CurrentMatches.Clear();
        gameStateService.SetState(GlobalEnums.GameState.move);
    }

    private async UniTask DelaySeconds(float seconds)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(seconds));
    }

    public HashSet<SC_Gem> GetCascadeGems()
    {
        return cascadeGems;
    }

    public HashSet<Vector2Int> GetCascadePositions()
    {
        return cascadePositions;
    }
}
