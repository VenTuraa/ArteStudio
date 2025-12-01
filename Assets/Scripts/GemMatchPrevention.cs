using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public interface IMatchPreventionStrategy
{
    SC_Gem GetSafeGemType(GameBoard gameBoard, Vector2Int position, SC_Gem[] availableGems);
}

public class GemMatchPrevention : IMatchPreventionStrategy
{
    public SC_Gem GetSafeGemType(GameBoard gameBoard, Vector2Int position, SC_Gem[] availableGems)
    {
        if (availableGems == null || availableGems.Length == 0)
            return null;

        var safeGems = availableGems.Where(gemPrefab => !gameBoard.MatchesAt(position, gemPrefab)).ToList();

        if (safeGems.Count > 0)
        {
            var bestSafeGems = safeGems.Where(gemPrefab => !HasPotentialMatchWithNeighbors(gameBoard, position, gemPrefab)).ToList();
            
            if (bestSafeGems.Count > 0)
            {
                return bestSafeGems[Random.Range(0, bestSafeGems.Count)];
            }
            
            return safeGems[Random.Range(0, safeGems.Count)];
        }
        
        return GetMinimumMatchGem(gameBoard, position, availableGems);
    }

    private bool HasPotentialMatchWithNeighbors(GameBoard gameBoard, Vector2Int position, SC_Gem gemPrefab)
    {
        GlobalEnums.GemType gemType = gemPrefab.type;

        if (position.x > 0)
        {
            SC_Gem left = gameBoard.GetGem(position.x - 1, position.y);
            if (left && left.type == gemType)
            {
                if (position.x > 1)
                {
                    SC_Gem left2 = gameBoard.GetGem(position.x - 2, position.y);
                    if (left2 && left2.type == gemType)
                        return true;
                }
            }
        }

        if (position.x < gameBoard.Width - 1)
        {
            SC_Gem right = gameBoard.GetGem(position.x + 1, position.y);
            if (right && right.type == gemType)
            {
                if (position.x < gameBoard.Width - 2)
                {
                    SC_Gem right2 = gameBoard.GetGem(position.x + 2, position.y);
                    if (right2 && right2.type == gemType)
                        return true;
                }
            }
        }

        if (position.y > 0)
        {
            SC_Gem below = gameBoard.GetGem(position.x, position.y - 1);
            if (below && below.type == gemType)
            {
                if (position.y > 1)
                {
                    SC_Gem below2 = gameBoard.GetGem(position.x, position.y - 2);
                    if (below2 && below2.type == gemType)
                        return true;
                }
            }
        }

        if (position.y < gameBoard.Height - 1)
        {
            SC_Gem above = gameBoard.GetGem(position.x, position.y + 1);
            if (above && above.type == gemType)
            {
                if (position.y < gameBoard.Height - 2)
                {
                    SC_Gem above2 = gameBoard.GetGem(position.x, position.y + 2);
                    if (above2 && above2.type == gemType)
                        return true;
                }
            }
        }

        return false;
    }

    private SC_Gem GetMinimumMatchGem(GameBoard gameBoard, Vector2Int position, SC_Gem[] availableGems)
    {
        var minMatchCount = int.MaxValue;
        var bestOptions = new List<SC_Gem>();

        foreach (SC_Gem gemPrefab in availableGems)
        {
            int matchCount = CountPotentialMatches(gameBoard, position, gemPrefab);
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

        return bestOptions.Count > 0 ? bestOptions[Random.Range(0, bestOptions.Count)] : availableGems[0];
    }
    
    private int CountPotentialMatches(GameBoard gameBoard, Vector2Int position, SC_Gem gemPrefab)
    {
        int matchGroupCount = 0;

        if (HasHorizontalMatch(gameBoard, position, gemPrefab))
            matchGroupCount++;

        if (HasVerticalMatch(gameBoard, position, gemPrefab))
            matchGroupCount++;

        return matchGroupCount;
    }
    
    private bool HasHorizontalMatch(GameBoard gameBoard, Vector2Int position, SC_Gem gemPrefab)
    {
        GlobalEnums.GemType gemType = gemPrefab.type;

        if (position.x > 1)
        {
            SC_Gem left1 = gameBoard.GetGem(position.x - 1, position.y);
            SC_Gem left2 = gameBoard.GetGem(position.x - 2, position.y);
            if (left1 && left2 && left1.type == gemType && left2.type == gemType)
                return true;
        }

        if (position.x < gameBoard.Width - 2)
        {
            SC_Gem right1 = gameBoard.GetGem(position.x + 1, position.y);
            SC_Gem right2 = gameBoard.GetGem(position.x + 2, position.y);
            if (right1 && right2 && right1.type == gemType && right2.type == gemType)
                return true;
        }

        if (position.x > 0 && position.x < gameBoard.Width - 1)
        {
            SC_Gem left = gameBoard.GetGem(position.x - 1, position.y);
            SC_Gem right = gameBoard.GetGem(position.x + 1, position.y);
            if (left && right && left.type == gemType && right.type == gemType)
                return true;
        }

        return false;
    }
    
    private bool HasVerticalMatch(GameBoard gameBoard, Vector2Int position, SC_Gem gemPrefab)
    {
        GlobalEnums.GemType gemType = gemPrefab.type;

        if (position.y > 1)
        {
            SC_Gem below1 = gameBoard.GetGem(position.x, position.y - 1);
            SC_Gem below2 = gameBoard.GetGem(position.x, position.y - 2);
            if (below1 && below2 && below1.type == gemType && below2.type == gemType)
                return true;
        }

        if (position.y < gameBoard.Height - 2)
        {
            SC_Gem above1 = gameBoard.GetGem(position.x, position.y + 1);
            SC_Gem above2 = gameBoard.GetGem(position.x, position.y + 2);
            if (above1 && above2 && above1.type == gemType && above2.type == gemType)
                return true;
        }

        if (position.y > 0 && position.y < gameBoard.Height - 1)
        {
            SC_Gem below = gameBoard.GetGem(position.x, position.y - 1);
            SC_Gem above = gameBoard.GetGem(position.x, position.y + 1);
            if (below && above && below.type == gemType && above.type == gemType)
                return true;
        }

        return false;
    }
}

