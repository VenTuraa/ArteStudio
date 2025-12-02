using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

public class BombLogicService : IBombHandler
{
    private const int MIN_SAME_COLOR_COUNT_FOR_BOMB_MATCH = 2;
    private const int MIN_REGULAR_COUNT_FOR_NEW_BOMB = 3;
    private const int CARDINAL_EXPLOSION_RADIUS = 2;
    private const float POST_EXPLOSION_DELAY = 0.1f;

    private static readonly Dictionary<GlobalEnums.GemType, Color> BombColorMap = new()
    {
        { GlobalEnums.GemType.blue, Color.blue },
        { GlobalEnums.GemType.green, Color.green },
        { GlobalEnums.GemType.red, Color.red },
        { GlobalEnums.GemType.yellow, Color.yellow },
        { GlobalEnums.GemType.purple, new Color(0.5f, 0f, 0.5f) }
    };

    private readonly IGameBoard gameBoard;
    private readonly SC_GameVariables gameVariables;
    private readonly IScoreService scoreService;
    private readonly IGemDestroyer gemDestroyer;
    private readonly IGemSpawnerService gemSpawner;
    private ICascadeService cascadeService;

    public void SetCascadeService(ICascadeService cascadeService)
    {
        this.cascadeService = cascadeService ?? throw new System.ArgumentNullException(nameof(cascadeService));
    }

    [Inject]
    public BombLogicService(
        IGameBoard gameBoard,
        SC_GameVariables gameVariables,
        IScoreService scoreService,
        IGemDestroyer gemDestroyer,
        IGemSpawnerService gemSpawner)
    {
        this.gameBoard = gameBoard ?? throw new System.ArgumentNullException(nameof(gameBoard));
        this.gameVariables = gameVariables ?? throw new System.ArgumentNullException(nameof(gameVariables));
        this.scoreService = scoreService ?? throw new System.ArgumentNullException(nameof(scoreService));
        this.gemDestroyer = gemDestroyer ?? throw new System.ArgumentNullException(nameof(gemDestroyer));
        this.gemSpawner = gemSpawner ?? throw new System.ArgumentNullException(nameof(gemSpawner));
    }

    public void CheckBombMatch(int x, int y, SC_Gem bombGem, List<SC_Gem> currentMatches)
    {
        if (!bombGem || bombGem.type != GlobalEnums.GemType.bomb)
            return;

        GlobalEnums.GemType bombColor = GetBombColor(bombGem);
        List<SC_Gem> adjacentGems = GetAdjacentGems(x, y);

        int matchedBombCount = CountBombMatches(adjacentGems, currentMatches, bombGem);
        int matchedSameColorCount = CountSameColorMatches(adjacentGems, currentMatches, bombGem, bombColor);

        bool shouldMatch = matchedBombCount >= 1 || matchedSameColorCount >= MIN_SAME_COLOR_COUNT_FOR_BOMB_MATCH;

        if (shouldMatch)
        {
            MarkBombAsMatched(bombGem, currentMatches);

            foreach (SC_Gem adjacentGem in adjacentGems)
            {
                if (adjacentGem && adjacentGem.type == GlobalEnums.GemType.bomb && !adjacentGem.isMatch)
                {
                    MarkBombAsMatched(adjacentGem, currentMatches);
                }
            }

            TryCreateNewBombFromMatch(x, y, bombColor, currentMatches);
        }
    }

    public void CheckBombToBombMatch(int x, int y, SC_Gem bombGem, List<SC_Gem> currentMatches)
    {
        if (!bombGem || bombGem.type != GlobalEnums.GemType.bomb)
            return;

        if (HasAdjacentBomb(x, y))
        {
            bombGem.isMatch = true;
            if (!currentMatches.Contains(bombGem))
            {
                currentMatches.Add(bombGem);
            }
        }
    }

    public GlobalEnums.GemType GetGemColorForMatch(SC_Gem gem)
    {
        if (gem == null)
            return GlobalEnums.GemType.blue;

        return gem.type == GlobalEnums.GemType.bomb ? gem.GemColor : gem.type;
    }

    public List<Vector2Int> GetBombExplosionPattern(Vector2Int bombPos)
    {
        List<Vector2Int> explosionPositions = new List<Vector2Int>();

        AddCardinalExplosionPositions(explosionPositions, bombPos);
        AddDiagonalExplosionPositions(explosionPositions, bombPos);

        return explosionPositions;
    }

    public void ApplyBombColor(SC_Gem bomb, GlobalEnums.GemType gemType)
    {
        if (!bomb || gemType == GlobalEnums.GemType.bomb)
            return;

        Color bombColor = BombColorMap.TryGetValue(gemType, out Color color) ? color : Color.white;
        bomb.Sprite.color = bombColor;
    }

    private GlobalEnums.GemType GetBombColor(SC_Gem bomb)
    {
        if (bomb == null || bomb.type != GlobalEnums.GemType.bomb)
            return GlobalEnums.GemType.blue;

        return bomb.GemColor;
    }

    private List<SC_Gem> GetAdjacentGems(int x, int y)
    {
        List<SC_Gem> adjacentGems = new List<SC_Gem>();

        if (IsValidPosition(x - 1, y)) adjacentGems.Add(gameBoard.GetGem(x - 1, y));
        if (IsValidPosition(x + 1, y)) adjacentGems.Add(gameBoard.GetGem(x + 1, y));
        if (IsValidPosition(x, y - 1)) adjacentGems.Add(gameBoard.GetGem(x, y - 1));
        if (IsValidPosition(x, y + 1)) adjacentGems.Add(gameBoard.GetGem(x, y + 1));

        return adjacentGems;
    }

    private bool IsValidPosition(int x, int y)
    {
        if (x < 0 || x >= gameBoard.Width || y < 0 || y >= gameBoard.Height)
            return false;

        return gameBoard.GetGem(x, y) != null;
    }

    private bool HasAdjacentBomb(int x, int y)
    {
        return IsBombAt(x - 1, y) ||
               IsBombAt(x + 1, y) ||
               IsBombAt(x, y - 1) ||
               IsBombAt(x, y + 1);
    }

    private bool IsBombAt(int x, int y)
    {
        SC_Gem gem = gameBoard.GetGem(x, y);
        return gem && gem.type == GlobalEnums.GemType.bomb;
    }

    private int CountBombMatches(List<SC_Gem> adjacentGems, List<SC_Gem> currentMatches, SC_Gem bombGem)
    {
        int count = 0;

        foreach (SC_Gem gem in adjacentGems)
        {
            if (gem && gem.type == GlobalEnums.GemType.bomb && (gem.isMatch || currentMatches.Contains(gem)))
                count++;
        }

        return count;
    }

    private int CountSameColorMatches(List<SC_Gem> adjacentGems, List<SC_Gem> currentMatches, SC_Gem bombGem,
        GlobalEnums.GemType bombColor)
    {
        int count = 0;

        foreach (SC_Gem gem in adjacentGems)
        {
            if (gem && gem.type != GlobalEnums.GemType.bomb)
            {
                if (gem.type == bombColor && (gem.isMatch || currentMatches.Contains(gem)))
                    count++;
            }
        }

        return count;
    }

    private void MarkBombAsMatched(SC_Gem bombGem, List<SC_Gem> currentMatches)
    {
        bombGem.isMatch = true;
        if (!currentMatches.Contains(bombGem))
        {
            currentMatches.Add(bombGem);
        }
    }

    private void TryCreateNewBombFromMatch(int x, int y, GlobalEnums.GemType bombColor, List<SC_Gem> currentMatches)
    {
        Dictionary<GlobalEnums.GemType, int> colorCounts = CountRegularGemsByColor(bombColor, currentMatches);

        if (colorCounts.Count == 0)
            return;

        GlobalEnums.GemType dominantColor = GlobalEnums.GemType.blue;
        int maxCount = 0;
        int totalCount = 0;

        foreach (var kvp in colorCounts)
        {
            totalCount += kvp.Value;
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                dominantColor = kvp.Key;
            }
        }

        if (totalCount >= MIN_REGULAR_COUNT_FOR_NEW_BOMB && maxCount > 0)
        {
            gameBoard.BombsToCreate.Add(new GameBoard.BombCreationInfo
            {
                position = new Vector2Int(x, y),
                gemType = dominantColor
            });
        }
    }

    private Dictionary<GlobalEnums.GemType, int> CountRegularGemsByColor(GlobalEnums.GemType bombColor,
        List<SC_Gem> currentMatches)
    {
        Dictionary<GlobalEnums.GemType, int> colorCounts = new Dictionary<GlobalEnums.GemType, int>();

        foreach (SC_Gem gem in currentMatches)
        {
            if (gem && gem.type != GlobalEnums.GemType.bomb && gem.type == bombColor)
            {
                colorCounts.TryAdd(gem.type, 0);
                colorCounts[gem.type]++;
            }
        }

        return colorCounts;
    }

    private void AddCardinalExplosionPositions(List<Vector2Int> positions, Vector2Int bombPos)
    {
        for (int offset = 1; offset <= CARDINAL_EXPLOSION_RADIUS; offset++)
        {
            if (bombPos.x - offset >= 0)
                positions.Add(new Vector2Int(bombPos.x - offset, bombPos.y));
        }

        for (int offset = 1; offset <= CARDINAL_EXPLOSION_RADIUS; offset++)
        {
            if (bombPos.x + offset < gameBoard.Width)
                positions.Add(new Vector2Int(bombPos.x + offset, bombPos.y));
        }

        for (int offset = 1; offset <= CARDINAL_EXPLOSION_RADIUS; offset++)
        {
            if (bombPos.y - offset >= 0)
                positions.Add(new Vector2Int(bombPos.x, bombPos.y - offset));
        }

        for (int offset = 1; offset <= CARDINAL_EXPLOSION_RADIUS; offset++)
        {
            if (bombPos.y + offset < gameBoard.Height)
                positions.Add(new Vector2Int(bombPos.x, bombPos.y + offset));
        }
    }

    private void AddDiagonalExplosionPositions(List<Vector2Int> positions, Vector2Int bombPos)
    {
        if (bombPos.x > 0 && bombPos.y > 0)
            positions.Add(new Vector2Int(bombPos.x - 1, bombPos.y - 1));

        if (bombPos.x < gameBoard.Width - 1 && bombPos.y > 0)
            positions.Add(new Vector2Int(bombPos.x + 1, bombPos.y - 1));

        if (bombPos.x > 0 && bombPos.y < gameBoard.Height - 1)
            positions.Add(new Vector2Int(bombPos.x - 1, bombPos.y + 1));

        if (bombPos.x < gameBoard.Width - 1 && bombPos.y < gameBoard.Height - 1)
            positions.Add(new Vector2Int(bombPos.x + 1, bombPos.y + 1));
    }

    public async UniTask HandleBombExplosions(List<SC_Gem> bombs, HashSet<SC_Gem> explodingBombs)
    {
        HashSet<SC_Gem> processedBombs = new HashSet<SC_Gem>();
        List<SC_Gem> bombsToExplodeNext = new List<SC_Gem>();

        foreach (SC_Gem bomb in bombs)
        {
            if (!IsValidBombForExplosion(bomb, processedBombs))
                continue;

            processedBombs.Add(bomb);
            explodingBombs.Add(bomb);

            await ProcessBombExplosion(bomb, processedBombs, bombsToExplodeNext, explodingBombs);
        }

        if (bombsToExplodeNext.Count > 0)
        {
            await HandleBombExplosions(bombsToExplodeNext, explodingBombs);
        }
        else
        {
            explodingBombs.Clear();
            await UniTask.Delay(System.TimeSpan.FromSeconds(POST_EXPLOSION_DELAY));
            if (cascadeService != null)
            {
                cascadeService.ProcessCascade().Forget();
            }
        }
    }

    private bool IsValidBombForExplosion(SC_Gem bomb, HashSet<SC_Gem> processedBombs)
    {
        if (bomb == null || !bomb.gameObject.activeInHierarchy || processedBombs.Contains(bomb))
            return false;

        Vector2Int bombPos = bomb.posIndex;
        SC_Gem bombAtPos = gameBoard.GetGem(bombPos.x, bombPos.y);
        return bombAtPos == bomb;
    }

    private async UniTask ProcessBombExplosion(SC_Gem bomb, HashSet<SC_Gem> processedBombs,
        List<SC_Gem> bombsToExplodeNext, HashSet<SC_Gem> explodingBombs)
    {
        Vector2Int bombPos = bomb.posIndex;
        var explosionPositions = GetBombExplosionPattern(bombPos);

        await UniTask.Delay(System.TimeSpan.FromSeconds(gameVariables.bombExplosionDelay));
        
        DestroyNeighborsInExplosion(bomb, explosionPositions, processedBombs, bombsToExplodeNext);
        DestroyBombIfStillPresent(bomb, bombPos);

        explodingBombs.Remove(bomb);
    }

    private void DestroyNeighborsInExplosion(SC_Gem bomb, List<Vector2Int> explosionPositions,
        HashSet<SC_Gem> processedBombs, List<SC_Gem> bombsToExplodeNext)
    {
        foreach (Vector2Int pos in explosionPositions)
        {
            SC_Gem gem = gameBoard.GetGem(pos.x, pos.y);
            if (gem && gem != bomb)
            {
                if (gem.type == GlobalEnums.GemType.bomb)
                {
                    if (!processedBombs.Contains(gem) && !bombsToExplodeNext.Contains(gem))
                    {
                        bombsToExplodeNext.Add(gem);
                    }
                }
                else
                {
                    scoreService.AddScore(gem);
                    gemDestroyer.DestroyGem(pos, g => g.type != GlobalEnums.GemType.bomb);
                }
            }
        }
    }

    private void DestroyBombIfStillPresent(SC_Gem bomb, Vector2Int bombPos)
    {
        SC_Gem bombAtPos = gameBoard.GetGem(bombPos.x, bombPos.y);
        if (bombAtPos == bomb)
        {
            scoreService.AddScore(bomb);
            gemDestroyer.DestroyGem(bombPos, g => g.type == GlobalEnums.GemType.bomb);
        }
    }

    public HashSet<Vector2Int> CreateBombsFromMatches()
    {
        HashSet<Vector2Int> newBombPositions = new HashSet<Vector2Int>();

        if (gameBoard.BombsToCreate.Count > 0)
        {
            foreach (var bombInfo in gameBoard.BombsToCreate)
            {
                CreateBombAt(bombInfo.position, bombInfo.gemType);
                newBombPositions.Add(bombInfo.position);
            }

            gameBoard.BombsToCreate.Clear();
        }

        return newBombPositions;
    }

    public void RemoveNewBombsFromMatches(HashSet<Vector2Int> newBombPositions, List<SC_Gem> currentMatches)
    {
        if (newBombPositions.Count > 0)
        {
            for (int i = currentMatches.Count - 1; i >= 0; i--)
            {
                SC_Gem gem = currentMatches[i];
                if (gem && gem.type == GlobalEnums.GemType.bomb)
                {
                    Vector2Int gemPos = gem.posIndex;
                    if (newBombPositions.Contains(gemPos))
                    {
                        gem.isMatch = false;
                        currentMatches.RemoveAt(i);
                    }
                }
            }
        }
    }

    private void CreateBombAt(Vector2Int position, GlobalEnums.GemType gemType)
    {
        SC_Gem existingGem = gameBoard.GetGem(position.x, position.y);
        
        if (existingGem && existingGem.type == GlobalEnums.GemType.bomb)
            return;
        
        if (existingGem && existingGem.isMatch)
        {
            gemDestroyer.DestroyGem(position, g => g.type != GlobalEnums.GemType.bomb);
        }

        SC_Gem bombPrefab = gameVariables.bomb;
        if (!bombPrefab)
            return;

        SC_Gem bomb = gemSpawner.SpawnGem(position, bombPrefab);
        if (bomb)
        {
            bomb.type = GlobalEnums.GemType.bomb;
            bomb.GemColor = gemType;
            bomb.isMatch = false;

            if (gameBoard.CurrentMatches.Contains(bomb))
            {
                gameBoard.CurrentMatches.Remove(bomb);
            }

            ApplyBombColor(bomb, gemType);
        }
    }
}