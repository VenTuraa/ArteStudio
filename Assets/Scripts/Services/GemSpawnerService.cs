using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

public class GemSpawnerService : IGemSpawnerService
{
    private const int MAX_MATCH_PREVENTION_ITERATIONS = 100;

    private readonly IGameBoard gameBoard;
    private readonly GemPool gemPool;
    private readonly SC_GameVariables gameVariables;
    private readonly IMatchPreventionStrategy matchPrevention;
    private readonly Transform gemsHolder;
    private IGameCoordinator gameCoordinator;

    [Inject]
    public GemSpawnerService(
        IGameBoard gameBoard,
        GemPool gemPool,
        SC_GameVariables gameVariables,
        IMatchPreventionStrategy matchPrevention,
        Transform gemsHolder)
    {
        this.gameBoard = gameBoard;
        this.gemPool = gemPool;
        this.gameVariables = gameVariables;
        this.matchPrevention = matchPrevention;
        this.gemsHolder = gemsHolder;
    }

    public void SetGameCoordinator(IGameCoordinator coordinator)
    {
        gameCoordinator = coordinator;
    }

    public void InitializeBoard(int width, int height)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CreateBackgroundTile(x, y);
                SC_Gem safeGem = GetInitialGemForPosition(x, y, MAX_MATCH_PREVENTION_ITERATIONS);
                SpawnGem(new Vector2Int(x, y), safeGem);
            }
        }
    }

    private void CreateBackgroundTile(int x, int y)
    {
        Vector2 position = new Vector2(x, y);
        GameObject bgTile = Object.Instantiate(gameVariables.bgTilePrefabs, position, Quaternion.identity);
        bgTile.transform.SetParent(gemsHolder);
        bgTile.name = "BG Tile - " + x + ", " + y;
    }

    private SC_Gem GetInitialGemForPosition(int x, int y, int maxIterations)
    {
        int gemIndex = Random.Range(0, gameVariables.gems.Length);
        int iterations = 0;

        while (gameBoard.MatchesAt(new Vector2Int(x, y), gameVariables.gems[gemIndex]) &&
               iterations < maxIterations)
        {
            gemIndex = Random.Range(0, gameVariables.gems.Length);
            iterations++;
        }

        return gameVariables.gems[gemIndex];
    }

    public SC_Gem SpawnGem(Vector2Int position, SC_Gem gemToSpawn)
    {
        Vector3 spawnPosition = new Vector3(position.x, position.y + gameVariables.dropHeight, 0f);
        SC_Gem gem = gemPool.GetGem(gemToSpawn, spawnPosition, gemsHolder);
        gem.name = "Gem - " + position.x + ", " + position.y;

        if (gem.type != GlobalEnums.GemType.bomb)
            gem.GemColor = gem.type;

        gameBoard.SetGem(position.x, position.y, gem);
        if (gameCoordinator is SC_GameLogic gameLogic)
        {
            gem.SetupGem(gameLogic, position);
        }

        return gem;
    }

    public SC_Gem GetSafeGemForPosition(int column, int targetY, List<GemDropInfo> existingDropQueue,
        List<GemDropInfo> newGemsQueue)
    {
        GameBoard simulatedBoard = CreateSimulatedBoardState(column, targetY, existingDropQueue, newGemsQueue);

        SC_Gem safeGem = matchPrevention.GetSafeGemType(
            simulatedBoard,
            new Vector2Int(column, targetY),
            gameVariables.gems
        );

        if (!safeGem || simulatedBoard.MatchesAt(new Vector2Int(column, targetY), safeGem))
            safeGem = GetGemWithMinimumMatches(simulatedBoard, column, targetY);

        if (!safeGem)
            safeGem = gameVariables.gems[Random.Range(0, gameVariables.gems.Length)];

        return safeGem;
    }

    private SC_Gem GetGemWithMinimumMatches(GameBoard simulatedBoard, int column, int targetY)
    {
        if (gameVariables == null || gameVariables.gems == null || gameVariables.gems.Length == 0)
            return null;

        int minMatchCount = int.MaxValue;
        List<SC_Gem> bestOptions = new List<SC_Gem>();

        foreach (SC_Gem gemPrefab in gameVariables.gems)
        {
            int matchCount = CountMatchesForGem(simulatedBoard, column, targetY, gemPrefab);
            if (matchCount < minMatchCount)
            {
                minMatchCount = matchCount;
                bestOptions.Clear();
                bestOptions.Add(gemPrefab);
            }
            else if (matchCount == minMatchCount)
            {
                bestOptions.Add(gemPrefab);
            }
        }

        return bestOptions.Count > 0 ? bestOptions[Random.Range(0, bestOptions.Count)] : gameVariables.gems[0];
    }

    private int CountMatchesForGem(GameBoard simulatedBoard, int column, int targetY, SC_Gem gemPrefab)
    {
        int matchCount = 0;
        GlobalEnums.GemType gemType = gemPrefab.type;

        // Check horizontal matches
        int horizontalCount = 1; // Count the gem itself
        // Check left
        for (int x = column - 1; x >= 0; x--)
        {
            SC_Gem gem = simulatedBoard.GetGem(x, targetY);
            if (gem && gem.type == gemType)
                horizontalCount++;
            else
                break;
        }

        // Check right
        for (int x = column + 1; x < simulatedBoard.Width; x++)
        {
            SC_Gem gem = simulatedBoard.GetGem(x, targetY);
            if (gem && gem.type == gemType)
                horizontalCount++;
            else
                break;
        }

        if (horizontalCount >= 3)
            matchCount++;

        // Check vertical matches
        int verticalCount = 1; // Count the gem itself
        // Check below
        for (int y = targetY - 1; y >= 0; y--)
        {
            SC_Gem gem = simulatedBoard.GetGem(column, y);
            if (gem && gem.type == gemType)
                verticalCount++;
            else
                break;
        }

        // Check above
        for (int y = targetY + 1; y < simulatedBoard.Height; y++)
        {
            SC_Gem gem = simulatedBoard.GetGem(column, y);
            if (gem && gem.type == gemType)
                verticalCount++;
            else
                break;
        }

        if (verticalCount >= 3)
            matchCount++;

        return matchCount;
    }

    private GameBoard CreateSimulatedBoardState(int column, int targetY, List<GemDropInfo> existingDropQueue,
        List<GemDropInfo> newGemsQueue)
    {
        GameBoard simulatedBoard = new GameBoard(gameBoard.Width, gameBoard.Height);
        Dictionary<int, SC_Gem> targetPositionToGem = BuildTargetPositionMap(existingDropQueue, newGemsQueue);
        HashSet<SC_Gem> fallingGems = GetFallingGemsSet(existingDropQueue);

        // First, copy all gems from the current board state
        for (int x = 0; x < gameBoard.Width; x++)
        {
            for (int y = 0; y < gameBoard.Height; y++)
            {
                if (x == column)
                {
                    // For the column being processed, use the simulated position logic
                    SC_Gem gemToPlace = GetGemForSimulatedPosition(column, y, targetPositionToGem, fallingGems);
                    simulatedBoard.SetGem(x, y, gemToPlace);
                }
                else
                {
                    // For other columns, use the current board state
                    SC_Gem existingGem = gameBoard.GetGem(x, y);
                    simulatedBoard.SetGem(x, y, existingGem);
                }
            }
        }

        return simulatedBoard;
    }

    private Dictionary<int, SC_Gem> BuildTargetPositionMap(List<GemDropInfo> existingDropQueue,
        List<GemDropInfo> newGemsQueue)
    {
        Dictionary<int, SC_Gem> positionMap = new Dictionary<int, SC_Gem>();

        foreach (var dropInfo in existingDropQueue.Concat(newGemsQueue))
        {
            if (dropInfo.gem && !positionMap.ContainsKey(dropInfo.targetY))
            {
                positionMap[dropInfo.targetY] = dropInfo.gem;
            }
        }

        return positionMap;
    }

    private HashSet<SC_Gem> GetFallingGemsSet(List<GemDropInfo> existingDropQueue)
    {
        HashSet<SC_Gem> fallingGems = new HashSet<SC_Gem>();

        foreach (var dropInfo in existingDropQueue)
        {
            if (dropInfo.gem && dropInfo.sourceY < gameBoard.Height)
            {
                fallingGems.Add(dropInfo.gem);
            }
        }

        return fallingGems;
    }

    private SC_Gem GetGemForSimulatedPosition(int column, int y, Dictionary<int, SC_Gem> targetPositionToGem,
        HashSet<SC_Gem> fallingGems)
    {
        if (targetPositionToGem.TryGetValue(y, out SC_Gem gemAtTarget))
        {
            return gemAtTarget;
        }

        SC_Gem existingGem = gameBoard.GetGem(column, y);
        if (existingGem && !fallingGems.Contains(existingGem))
        {
            return existingGem;
        }

        return null;
    }

    public SC_Gem SpawnGemAtPosition(int column, int spawnY, SC_Gem gemPrefab)
    {
        Vector3 spawnPosition = new Vector3(column, spawnY + gameVariables.dropHeight, 0f);
        SC_Gem newGem = gemPool.GetGem(gemPrefab, spawnPosition, gemsHolder);
        newGem.name = "Gem - " + column + ", " + spawnY;
        return newGem;
    }
}