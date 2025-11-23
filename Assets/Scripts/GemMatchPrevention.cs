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

        // Try to find a gem type that won't create matches
        List<SC_Gem> safeGems = availableGems.Where(gemPrefab => !gameBoard.MatchesAt(position, gemPrefab)).ToList();

        // If we have safe options, return a random one
        if (safeGems.Count > 0)
        {
            return safeGems[Random.Range(0, safeGems.Count)];
        }

        // If all gems would create matches, return the one that creates minimum matches
        return GetMinimumMatchGem(gameBoard, position, availableGems);
    }

    /// <summary>
    /// When matches are unavoidable, selects the gem type that creates the minimum number of matches.
    /// </summary>
    private SC_Gem GetMinimumMatchGem(GameBoard gameBoard, Vector2Int position, SC_Gem[] availableGems)
    {
        int minMatchCount = int.MaxValue;
        List<SC_Gem> bestOptions = new List<SC_Gem>();

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

        // Return a random gem from the best options (minimum matches)
        return bestOptions.Count > 0 ? bestOptions[Random.Range(0, bestOptions.Count)] : availableGems[0];
    }
    
    private int CountPotentialMatches(GameBoard gameBoard, Vector2Int position, SC_Gem gemPrefab)
    {
        int matchGroupCount = 0;

        // Check horizontal match using GameBoard.MatchesAt logic
        if (HasHorizontalMatch(gameBoard, position, gemPrefab))
            matchGroupCount++;

        // Check vertical match using GameBoard.MatchesAt logic
        if (HasVerticalMatch(gameBoard, position, gemPrefab))
            matchGroupCount++;

        return matchGroupCount;
    }
    
    private bool HasHorizontalMatch(GameBoard gameBoard, Vector2Int position, SC_Gem gemPrefab)
    {
        GlobalEnums.GemType gemType = gemPrefab.type;

        // Check left side 
        if (position.x > 1)
        {
            SC_Gem left1 = gameBoard.GetGem(position.x - 1, position.y);
            SC_Gem left2 = gameBoard.GetGem(position.x - 2, position.y);
            if (left1 && left2 && left1.type == gemType && left2.type == gemType)
                return true;
        }

        // Check right side 
        if (position.x < gameBoard.Width - 2)
        {
            SC_Gem right1 = gameBoard.GetGem(position.x + 1, position.y);
            SC_Gem right2 = gameBoard.GetGem(position.x + 2, position.y);
            if (right1 && right2 && right1.type == gemType && right2.type == gemType)
                return true;
        }

        // Check if gem would connect two groups 
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

        // Check below 
        if (position.y > 1)
        {
            SC_Gem below1 = gameBoard.GetGem(position.x, position.y - 1);
            SC_Gem below2 = gameBoard.GetGem(position.x, position.y - 2);
            if (below1 && below2 && below1.type == gemType && below2.type == gemType)
                return true;
        }

        // Check above
        if (position.y < gameBoard.Height - 2)
        {
            SC_Gem above1 = gameBoard.GetGem(position.x, position.y + 1);
            SC_Gem above2 = gameBoard.GetGem(position.x, position.y + 2);
            if (above1 && above2 && above1.type == gemType && above2.type == gemType)
                return true;
        }

        // Check if gem would connect two groups
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

