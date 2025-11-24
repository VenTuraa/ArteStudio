using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameBoard
{
    #region Variables

    private int height = 0;
    public int Height { get { return height; } }

    private int width = 0;
    public int Width { get { return width; } }
  
    private SC_Gem[,] allGems;
  //  public Gem[,] AllGems { get { return allGems; } }

    private int score = 0;
    public int Score 
    {
        get { return score; }
        set { score = value; }
    }

    private List<SC_Gem> currentMatches = new List<SC_Gem>();
    public List<SC_Gem> CurrentMatches { get { return currentMatches; } }
    
    private List<BombCreationInfo> bombsToCreate = new List<BombCreationInfo>();
    public List<BombCreationInfo> BombsToCreate { get { return bombsToCreate; } }
    
    public struct BombCreationInfo
    {
        public Vector2Int position;
        public GlobalEnums.GemType gemType;
    }
    #endregion

    public GameBoard(int _Width, int _Height)
    {
        height = _Height;
        width = _Width;
        allGems = new SC_Gem[width, height];
    }
    public bool MatchesAt(Vector2Int _PositionToCheck, SC_Gem _GemToCheck)
    {
        if (_GemToCheck == null)
            return false;
        
        if (!IsValidBoardPosition(_PositionToCheck.x, _PositionToCheck.y))
            return false;

        // Check horizontal match (left side)
        if (_PositionToCheck.x > 1)
        {
            SC_Gem left1 = GetGem(_PositionToCheck.x - 1, _PositionToCheck.y);
            SC_Gem left2 = GetGem(_PositionToCheck.x - 2, _PositionToCheck.y);
            if (left1 != null && left2 != null &&
                left1.type == _GemToCheck.type && left2.type == _GemToCheck.type)
                return true;
        }

        // Check vertical match (below)
        if (_PositionToCheck.y > 1)
        {
            SC_Gem below1 = GetGem(_PositionToCheck.x, _PositionToCheck.y - 1);
            SC_Gem below2 = GetGem(_PositionToCheck.x, _PositionToCheck.y - 2);
            if (below1 != null && below2 != null &&
                below1.type == _GemToCheck.type && below2.type == _GemToCheck.type)
                return true;
        }

        return false;
    }
    
    public void SetGem(int _X, int _Y, SC_Gem _Gem)
    {
        if (!IsValidBoardPosition(_X, _Y))
            return;
        
        allGems[_X, _Y] = _Gem;
    }
    
    public SC_Gem GetGem(int _X, int _Y)
    {
        if (!IsValidBoardPosition(_X, _Y))
            return null;
        
        return allGems[_X, _Y];
    }
    
    private bool IsValidBoardPosition(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    private BombLogicService bombLogicService;
    
    public void SetBombLogicService(BombLogicService service)
    {
        bombLogicService = service;
    }
    
    public void FindAllMatches()
    {
        currentMatches.Clear();
        bombsToCreate.Clear();
        
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                SC_Gem currentGem = allGems[x, y];
                if (currentGem)
                {
                    CheckHorizontalMatch(x, y, currentGem);
                    CheckVerticalMatch(x, y, currentGem);
                }
            }

        if (currentMatches.Count > 0)
            currentMatches = currentMatches.Distinct().ToList();

        // Also check for bomb-to-bomb matches (any color bombs match with each other)
        if (bombLogicService != null)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    SC_Gem currentGem = allGems[x, y];
                    if (currentGem != null && currentGem.type == GlobalEnums.GemType.bomb)
                    {
                        bombLogicService.CheckBombToBombMatch(x, y, currentGem, currentMatches);
                    }
                }
        }

        if (currentMatches.Count > 0)
            currentMatches = currentMatches.Distinct().ToList();

        CheckForBombs();
    }
    
    
    /// <summary>
    /// Gets the color of a gem for matching purposes.
    /// For bombs: returns GemColor; for regular gems: returns type.
    /// </summary>
    private GlobalEnums.GemType GetGemColorForMatch(SC_Gem gem)
    {
        if (bombLogicService != null)
        {
            return bombLogicService.GetGemColorForMatch(gem);
        }
        
        // Fallback to original logic if service not available
        if (gem == null)
            return GlobalEnums.GemType.blue;
            
        return gem.type == GlobalEnums.GemType.bomb ? gem.GemColor : gem.type;
    }
    
    /// <summary>
    /// Checks if a bomb should match based on adjacent gems and current matches.
    /// Delegates to BombLogicService if available.
    /// </summary>
    private void CheckBombMatch(int x, int y, SC_Gem bombGem)
    {
        bombLogicService?.CheckBombMatch(x, y, bombGem, currentMatches);
    }
    
    private void CheckHorizontalMatch(int x, int y, SC_Gem currentGem)
    {
        if (currentGem == null)
            return;
            
        // Get the color to match
        // For bombs: use GemColor; for regular gems: use type
        GlobalEnums.GemType colorToMatch = GetGemColorForMatch(currentGem);
        
        List<SC_Gem> matchGroup = new List<SC_Gem> { currentGem };
        Vector2Int matchStartPos = new Vector2Int(x, y);
        
        // Check left
        for (int i = x - 1; i >= 0; i--)
        {
            SC_Gem gem = allGems[i, y];
            if (gem != null && GetGemColorForMatch(gem) == colorToMatch)
            {
                matchGroup.Add(gem);
                matchStartPos = new Vector2Int(i, y);
            }
            else
                break;
        }
        
        // Check right
        for (int i = x + 1; i < width; i++)
        {
            SC_Gem gem = allGems[i, y];
            if (gem != null && GetGemColorForMatch(gem) == colorToMatch)
            {
                matchGroup.Add(gem);
            }
            else
                break;
        }
        
        if (matchGroup.Count >= 3)
        {
            foreach (SC_Gem gem in matchGroup)
            {
                gem.isMatch = true;
                if (!currentMatches.Contains(gem))
                {
                    currentMatches.Add(gem);
                }
            }
            
            if (matchGroup.Count >= 4)
            {
                // Use the color of the match group for bomb creation
                bombsToCreate.Add(new BombCreationInfo
                {
                    position = matchStartPos,
                    gemType = colorToMatch
                });
            }
        }
    }
    
    private void CheckVerticalMatch(int x, int y, SC_Gem currentGem)
    {
        if (currentGem == null)
            return;
            
        // Get the color to match
        // For bombs: use GemColor; for regular gems: use type
        GlobalEnums.GemType colorToMatch = GetGemColorForMatch(currentGem);
        
        List<SC_Gem> matchGroup = new List<SC_Gem> { currentGem };
        Vector2Int matchStartPos = new Vector2Int(x, y);
        
        // Check down
        for (int i = y - 1; i >= 0; i--)
        {
            SC_Gem gem = allGems[x, i];
            if (gem != null && GetGemColorForMatch(gem) == colorToMatch)
            {
                matchGroup.Add(gem);
                matchStartPos = new Vector2Int(x, i);
            }
            else
                break;
        }
        
        // Check up
        for (int i = y + 1; i < height; i++)
        {
            SC_Gem gem = allGems[x, i];
            if (gem != null && GetGemColorForMatch(gem) == colorToMatch)
            {
                matchGroup.Add(gem);
            }
            else
                break;
        }
        
        if (matchGroup.Count >= 3)
        {
            foreach (SC_Gem gem in matchGroup)
            {
                gem.isMatch = true;
                if (!currentMatches.Contains(gem))
                {
                    currentMatches.Add(gem);
                }
            }
            
            if (matchGroup.Count >= 4)
            {
                // Use the color of the match group for bomb creation
                bombsToCreate.Add(new BombCreationInfo
                {
                    position = matchStartPos,
                    gemType = colorToMatch
                });
            }
        }
    }
    
    public void CheckForBombs()
    {
        if (bombLogicService == null)
            return;
        
        // Check all bombs for matching conditions
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SC_Gem bomb = allGems[x, y];
                if (bomb != null && bomb.type == GlobalEnums.GemType.bomb && !bomb.isMatch)
                {
                    // Check if bomb should match (bomb with bomb, or bomb with 2+ same color)
                    CheckBombMatch(x, y, bomb);
                }
            }
        }
    }

    public void MarkBombArea(Vector2Int bombPos, int _BlastSize)
    {
        string _print = "";
        for (int x = bombPos.x - _BlastSize; x <= bombPos.x + _BlastSize; x++)
        {
            for (int y = bombPos.y - _BlastSize; y <= bombPos.y + _BlastSize; y++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    if (allGems[x, y] != null)
                    {
                        _print += "(" + x + "," + y + ")" + System.Environment.NewLine;
                        allGems[x, y].isMatch = true;
                        currentMatches.Add(allGems[x, y]);
                    }
                }
            }
        }
        currentMatches = currentMatches.Distinct().ToList();
    }
    
    public List<Vector2Int> GetBombExplosionPattern(Vector2Int bombPos)
    {
        if (bombLogicService != null)
        {
            return bombLogicService.GetBombExplosionPattern(bombPos);
        }
        
        // Fallback to empty list if service not available
        return new List<Vector2Int>();
    }
}

