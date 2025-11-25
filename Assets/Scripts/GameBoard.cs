using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameBoard
{
    private enum MatchDirection
    {
        Horizontal,
        Vertical
    }
    
    #region Variables

    private int height;
    public int Height => height;

    private int width = 0;
    public int Width => width;

    private SC_Gem[,] allGems;
    //  public Gem[,] AllGems { get { return allGems; } }

    public int Score { get; set; }

    private List<SC_Gem> currentMatches = new();
    public List<SC_Gem> CurrentMatches => currentMatches;

    private List<BombCreationInfo> bombsToCreate = new();
    public List<BombCreationInfo> BombsToCreate => bombsToCreate;

    private HashSet<SC_Gem> activeGems = new();
    public HashSet<SC_Gem> ActiveGems => activeGems;

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
        if (!_GemToCheck)
            return false;

        if (!IsValidBoardPosition(_PositionToCheck.x, _PositionToCheck.y))
            return false;

        // Check horizontal match (left side)
        if (_PositionToCheck.x > 1)
        {
            SC_Gem left1 = GetGem(_PositionToCheck.x - 1, _PositionToCheck.y);
            SC_Gem left2 = GetGem(_PositionToCheck.x - 2, _PositionToCheck.y);
            if (left1 && left2 &&
                left1.type == _GemToCheck.type && left2.type == _GemToCheck.type)
                return true;
        }

        // Check vertical match (below)
        if (_PositionToCheck.y > 1)
        {
            SC_Gem below1 = GetGem(_PositionToCheck.x, _PositionToCheck.y - 1);
            SC_Gem below2 = GetGem(_PositionToCheck.x, _PositionToCheck.y - 2);
            if (below1 && below2 &&
                below1.type == _GemToCheck.type && below2.type == _GemToCheck.type)
                return true;
        }

        return false;
    }

    public void SetGem(int _X, int _Y, SC_Gem gem)
    {
        if (!IsValidBoardPosition(_X, _Y))
            return;

        // Remove old gem from active gems if it exists
        SC_Gem oldGem = allGems[_X, _Y];
        if (oldGem && oldGem != gem)
        {
            activeGems.Remove(oldGem);
        }

        allGems[_X, _Y] = gem;

        // Add new gem to active gems
        if (gem)
        {
            activeGems.Add(gem);
        }
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
                if (currentGem && currentGem.type == GlobalEnums.GemType.bomb)
                {
                    bombLogicService.CheckBombToBombMatch(x, y, currentGem, currentMatches);
                }
            }
        }

        if (currentMatches.Count > 0)
            currentMatches = currentMatches.Distinct().ToList();

        CheckForBombs();
    }
    
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
    
    private void CheckBombMatch(int x, int y, SC_Gem bombGem)
    {
        bombLogicService?.CheckBombMatch(x, y, bombGem, currentMatches);
    }

    private void CheckHorizontalMatch(int x, int y, SC_Gem currentGem)
    {
        CheckMatchInDirection(x, y, currentGem, MatchDirection.Horizontal);
    }

    private void CheckVerticalMatch(int x, int y, SC_Gem currentGem)
    {
        CheckMatchInDirection(x, y, currentGem, MatchDirection.Vertical);
    }
    
    private void CheckMatchInDirection(int x, int y, SC_Gem currentGem, MatchDirection direction)
    {
        if (currentGem == null)
            return;

        const int MIN_MATCH_COUNT = 3;
        const int BOMB_CREATION_MATCH_COUNT = 4;
        
        GlobalEnums.GemType colorToMatch = GetGemColorForMatch(currentGem);
        List<SC_Gem> matchGroup = BuildMatchGroupInDirection(x, y, colorToMatch, direction);
        Vector2Int matchStartPos = GetMatchStartPositionInDirection(x, y, colorToMatch, direction);

        if (matchGroup.Count >= MIN_MATCH_COUNT)
        {
            MarkMatchGroupAsMatched(matchGroup);
            
            if (matchGroup.Count >= BOMB_CREATION_MATCH_COUNT)
            {
                TryCreateBombAtPosition(matchStartPos, colorToMatch);
            }
        }
    }
    
    private List<SC_Gem> BuildMatchGroupInDirection(int x, int y, GlobalEnums.GemType colorToMatch, MatchDirection direction)
    {
        List<SC_Gem> matchGroup = new List<SC_Gem> { allGems[x, y] };
        bool isHorizontal = direction == MatchDirection.Horizontal;

        int primaryCoord = isHorizontal ? x : y;
        int secondaryCoord = isHorizontal ? y : x;
        int maxBound = isHorizontal ? width : height;

        for (int i = primaryCoord - 1; i >= 0; i--)
        {
            SC_Gem gem = GetGemAtDirection(i, secondaryCoord, isHorizontal);
            if (gem && GetGemColorForMatch(gem) == colorToMatch)
            {
                matchGroup.Insert(0, gem);
            }
            else
                break;
        }

        for (int i = primaryCoord + 1; i < maxBound; i++)
        {
            SC_Gem gem = GetGemAtDirection(i, secondaryCoord, isHorizontal);
            if (gem && GetGemColorForMatch(gem) == colorToMatch)
            {
                matchGroup.Add(gem);
            }
            else
                break;
        }
        
        return matchGroup;
    }
    
    private SC_Gem GetGemAtDirection(int primaryCoord, int secondaryCoord, bool isHorizontal)
    {
        return isHorizontal ? allGems[primaryCoord, secondaryCoord] : allGems[secondaryCoord, primaryCoord];
    }
    
    private Vector2Int GetMatchStartPositionInDirection(int x, int y, GlobalEnums.GemType colorToMatch, MatchDirection direction)
    {
        Vector2Int matchStartPos = new Vector2Int(x, y);
        bool isHorizontal = direction == MatchDirection.Horizontal;

        int primaryCoord = isHorizontal ? x : y;
        int secondaryCoord = isHorizontal ? y : x;

        for (int i = primaryCoord - 1; i >= 0; i--)
        {
            SC_Gem gem = GetGemAtDirection(i, secondaryCoord, isHorizontal);
            if (gem && GetGemColorForMatch(gem) == colorToMatch)
            {
                matchStartPos = isHorizontal ? new Vector2Int(i, secondaryCoord) : new Vector2Int(secondaryCoord, i);
            }
            else
                break;
        }
        
        return matchStartPos;
    }
    
    private void MarkMatchGroupAsMatched(List<SC_Gem> matchGroup)
    {
        foreach (SC_Gem gem in matchGroup)
        {
            gem.isMatch = true;
            if (!currentMatches.Contains(gem))
            {
                currentMatches.Add(gem);
            }
        }
    }
    
    private void TryCreateBombAtPosition(Vector2Int position, GlobalEnums.GemType gemType)
    {
        bool alreadyExists = bombsToCreate.Any(b => b.position == position);
        if (!alreadyExists)
        {
            bombsToCreate.Add(new BombCreationInfo
            {
                position = position,
                gemType = gemType
            });
        }
    }

    private void CheckForBombs()
    {
        if (bombLogicService == null)
            return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SC_Gem bomb = allGems[x, y];
                if (bomb && bomb.type == GlobalEnums.GemType.bomb && !bomb.isMatch)
                {
                    CheckBombMatch(x, y, bomb);
                }
            }
        }
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