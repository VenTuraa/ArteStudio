using System.Collections.Generic;
using UnityEngine;

public class BombLogicService
{
    private const int MIN_SAME_COLOR_COUNT_FOR_BOMB_MATCH = 2;
    private const int MIN_REGULAR_COUNT_FOR_NEW_BOMB = 3;
    private const int CARDINAL_EXPLOSION_RADIUS = 2;
    
    private static readonly Dictionary<GlobalEnums.GemType, Color> BombColorMap = new()
    {
        { GlobalEnums.GemType.blue, Color.blue },
        { GlobalEnums.GemType.green, Color.green },
        { GlobalEnums.GemType.red, Color.red },
        { GlobalEnums.GemType.yellow, Color.yellow },
        { GlobalEnums.GemType.purple, new Color(0.5f, 0f, 0.5f) }
    };
    
    private readonly GameBoard gameBoard;
    
    public BombLogicService(GameBoard gameBoard)
    {
        this.gameBoard = gameBoard ?? throw new System.ArgumentNullException(nameof(gameBoard));
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
                if (adjacentGem != null && adjacentGem.type == GlobalEnums.GemType.bomb && !adjacentGem.isMatch)
                {
                    MarkBombAsMatched(adjacentGem, currentMatches);
                }
            }
            
            TryCreateNewBombFromMatch(x, y, bombColor, currentMatches);
        }
    }
    
    public void CheckBombToBombMatch(int x, int y, SC_Gem bombGem, List<SC_Gem> currentMatches)
    {
        if (bombGem == null || bombGem.type != GlobalEnums.GemType.bomb)
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
            
        if (gem.type == GlobalEnums.GemType.bomb)
        {
            return gem.GemColor;
        }

        return gem.type;
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
        if (bomb == null || gemType == GlobalEnums.GemType.bomb)
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
        return gem != null && gem.type == GlobalEnums.GemType.bomb;
    }
    
    private int CountBombMatches(List<SC_Gem> adjacentGems, List<SC_Gem> currentMatches, SC_Gem bombGem)
    {
        int count = 0;
        
        foreach (SC_Gem gem in adjacentGems)
        {
            if (gem != null && gem.type == GlobalEnums.GemType.bomb)
                count++;
        }
        
        foreach (SC_Gem gem in currentMatches)
        {
            if (gem != null && gem != bombGem && gem.type == GlobalEnums.GemType.bomb && !adjacentGems.Contains(gem))
                count++;
        }
        
        return count;
    }
    
    private int CountSameColorMatches(List<SC_Gem> adjacentGems, List<SC_Gem> currentMatches, SC_Gem bombGem, GlobalEnums.GemType bombColor)
    {
        int count = 0;
        
        foreach (SC_Gem gem in adjacentGems)
        {
            if (gem != null && gem.type != GlobalEnums.GemType.bomb)
            {
                if (gem.type == bombColor && (gem.isMatch || currentMatches.Contains(gem)))
                    count++;
            }
        }
        
        foreach (SC_Gem gem in currentMatches)
        {
            if (gem != null && gem != bombGem && gem.type != GlobalEnums.GemType.bomb && !adjacentGems.Contains(gem))
            {
                if (gem.type == bombColor)
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
    
    private Dictionary<GlobalEnums.GemType, int> CountRegularGemsByColor(GlobalEnums.GemType bombColor, List<SC_Gem> currentMatches)
    {
        Dictionary<GlobalEnums.GemType, int> colorCounts = new Dictionary<GlobalEnums.GemType, int>();
        
        foreach (SC_Gem gem in currentMatches)
        {
            if (gem != null && gem.type != GlobalEnums.GemType.bomb && gem.type == bombColor)
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
}

