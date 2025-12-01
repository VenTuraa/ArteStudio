using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public class GameBoard : IGameBoard
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

    public List<SC_Gem> CurrentMatches { get; private set; } = new();

    public List<BombCreationInfo> BombsToCreate { get; } = new();

    public HashSet<SC_Gem> ActiveGems { get; } = new();

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

        if (_PositionToCheck.x > 1)
        {
            SC_Gem left1 = GetGem(_PositionToCheck.x - 1, _PositionToCheck.y);
            SC_Gem left2 = GetGem(_PositionToCheck.x - 2, _PositionToCheck.y);
            if (left1 && left2 &&
                left1.type == _GemToCheck.type && left2.type == _GemToCheck.type)
                return true;
        }

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

        SC_Gem oldGem = allGems[_X, _Y];
        if (oldGem && oldGem != gem)
            ActiveGems.Remove(oldGem);

        allGems[_X, _Y] = gem;

        if (gem)
            ActiveGems.Add(gem);
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

    private IBombHandler bombHandler;

    public void SetBombHandler(IBombHandler handler)
    {
        bombHandler = handler;
    }

    public void FindAllMatches()
    {
        CurrentMatches.Clear();
        BombsToCreate.Clear();

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

        if (CurrentMatches.Count > 0)
            CurrentMatches = CurrentMatches.Distinct().ToList();

        if (bombHandler != null)
        {
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                SC_Gem currentGem = allGems[x, y];
                if (currentGem && currentGem.type == GlobalEnums.GemType.bomb)
                {
                    bombHandler.CheckBombToBombMatch(x, y, currentGem, CurrentMatches);
                }
            }
        }

        if (CurrentMatches.Count > 0)
            CurrentMatches = CurrentMatches.Distinct().ToList();

        CheckForBombs();
    }
    
    private GlobalEnums.GemType GetGemColorForMatch(SC_Gem gem)
    {
        if (bombHandler != null)
        {
            return bombHandler.GetGemColorForMatch(gem);
        }

        if (!gem)
            return GlobalEnums.GemType.blue;

        return gem.type == GlobalEnums.GemType.bomb ? gem.GemColor : gem.type;
    }
    
    private void CheckBombMatch(int x, int y, SC_Gem bombGem)
    {
        bombHandler?.CheckBombMatch(x, y, bombGem, CurrentMatches);
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
        if (!currentGem)
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
            if (!CurrentMatches.Contains(gem))
            {
                CurrentMatches.Add(gem);
            }
        }
    }
    
    private void TryCreateBombAtPosition(Vector2Int position, GlobalEnums.GemType gemType)
    {
        bool alreadyExists = BombsToCreate.Any(b => b.position == position);
        if (!alreadyExists)
        {
            BombsToCreate.Add(new BombCreationInfo
            {
                position = position,
                gemType = gemType
            });
        }
    }

    private void CheckForBombs()
    {
        if (bombHandler == null)
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
    
    public class Factory : PlaceholderFactory<int, int, GameBoard>
    {
    }
}