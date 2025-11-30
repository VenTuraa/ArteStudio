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
    private SC_GameLogic gameLogic;

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

    public void SetGameLogic(SC_GameLogic logic)
    {
        gameLogic = logic;
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
        if (gameLogic)
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

        if (!safeGem)
        {
            safeGem = gameVariables.gems[Random.Range(0, gameVariables.gems.Length)];
        }

        return safeGem;
    }

    private GameBoard CreateSimulatedBoardState(int column, int targetY, List<GemDropInfo> existingDropQueue,
        List<GemDropInfo> newGemsQueue)
    {
        GameBoard simulatedBoard = new GameBoard(gameBoard.Width, gameBoard.Height);
        Dictionary<int, SC_Gem> targetPositionToGem = BuildTargetPositionMap(existingDropQueue, newGemsQueue);
        HashSet<SC_Gem> fallingGems = GetFallingGemsSet(existingDropQueue);

        for (int x = 0; x < gameBoard.Width; x++)
        {
            for (int y = 0; y < gameBoard.Height; y++)
            {
                SC_Gem gemToPlace = x == column
                    ? GetGemForSimulatedPosition(column, y, targetPositionToGem, fallingGems)
                    : gameBoard.GetGem(x, y);

                simulatedBoard.SetGem(x, y, gemToPlace);
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
