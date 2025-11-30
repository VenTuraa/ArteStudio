using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

public class MatchHandlerService : IMatchHandlerService
{
    private static readonly Vector2Int[] NEIGHBOR_DIRECTIONS = new Vector2Int[]
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1)
    };

    private readonly IGameBoard gameBoard;
    private readonly IScoreService scoreService;
    private readonly IGemDestroyer gemDestroyer;
    private IBombHandler bombHandler;
    private ICascadeService cascadeService;

    public void SetBombHandler(IBombHandler bombHandler)
    {
        this.bombHandler = bombHandler ?? throw new System.ArgumentNullException(nameof(bombHandler));
    }

    public void SetCascadeService(ICascadeService cascadeService)
    {
        this.cascadeService = cascadeService ?? throw new System.ArgumentNullException(nameof(cascadeService));
    }

    [Inject]
    public MatchHandlerService(
        IGameBoard gameBoard,
        IScoreService scoreService,
        IGemDestroyer gemDestroyer)
    {
        this.gameBoard = gameBoard;
        this.scoreService = scoreService;
        this.gemDestroyer = gemDestroyer;
    }

    public void DestroyMatches()
    {
        if (bombHandler == null || cascadeService == null)
            return;

        var (bombsToExplode, regularMatches) = SeparateBombsFromRegularMatches();

        DestroyRegularMatches(regularMatches);

        var newBombPositions = bombHandler.CreateBombsFromMatches();
        bombHandler.RemoveNewBombsFromMatches(newBombPositions, gameBoard.CurrentMatches);

        if (bombsToExplode.Count > 0)
        {
            bombHandler.HandleBombExplosions(bombsToExplode, new HashSet<SC_Gem>()).Forget();
        }
        else
        {
            cascadeService.ProcessCascade().Forget();
        }
    }

    private (List<SC_Gem> bombs, List<SC_Gem> regular) SeparateBombsFromRegularMatches()
    {
        List<SC_Gem> bombsToExplode = new List<SC_Gem>();
        List<SC_Gem> regularMatches = new List<SC_Gem>();

        for (int i = 0; i < gameBoard.CurrentMatches.Count; i++)
        {
            SC_Gem gem = gameBoard.CurrentMatches[i];
            if (gem && gem.isMatch)
            {
                if (gem.type == GlobalEnums.GemType.bomb)
                    bombsToExplode.Add(gem);
                else
                    regularMatches.Add(gem);
            }
        }

        return (bombsToExplode, regularMatches);
    }

    private void DestroyRegularMatches(List<SC_Gem> regularMatches)
    {
        foreach (SC_Gem gem in regularMatches)
        {
            if (gem && gem.type != GlobalEnums.GemType.bomb && gem.isMatch)
            {
                scoreService.AddScore(gem);
                gemDestroyer.DestroyGem(gem.posIndex, g => g.type != GlobalEnums.GemType.bomb);
            }
        }
    }

    public List<SC_Gem> FilterValidCascadeMatches(List<SC_Gem> allMatches)
    {
        if (cascadeService == null)
            return new List<SC_Gem>();

        HashSet<SC_Gem> cascadeGems = cascadeService.GetCascadeGems();
        HashSet<Vector2Int> cascadePositions = cascadeService.GetCascadePositions();

        if (allMatches.Count == 0 || (cascadeGems.Count == 0 && cascadePositions.Count == 0))
            return new List<SC_Gem>();

        HashSet<SC_Gem> validMatchGems = new HashSet<SC_Gem>();
        HashSet<SC_Gem> processedGems = new HashSet<SC_Gem>();

        foreach (SC_Gem gem in allMatches)
        {
            if (!gem || processedGems.Contains(gem))
                continue;

            List<SC_Gem> matchGroup = BuildMatchGroupFromGem(gem, allMatches);
            CascadeMatchValidation validation = ValidateMatchGroup(matchGroup, cascadeGems, cascadePositions);

            if (validation.IsValid)
                AddMatchGroupToValid(validMatchGems, processedGems, matchGroup);
            else
                MarkMatchGroupAsProcessed(processedGems, matchGroup);
        }

        if (validMatchGems.Count == 0 && cascadePositions.Count > 0 && allMatches.Count > 0)
        {
            foreach (SC_Gem matchGem in allMatches)
            {
                if (!matchGem || processedGems.Contains(matchGem))
                    continue;

                if (cascadePositions.Contains(matchGem.posIndex))
                {
                    List<SC_Gem> matchGroup = BuildMatchGroupFromGem(matchGem, allMatches);
                    AddMatchGroupToValid(validMatchGems, processedGems, matchGroup);
                }
            }
        }

        return validMatchGems.ToList();
    }

    public void ProcessCascadeMatches(List<SC_Gem> validCascadeMatches)
    {
        gameBoard.CurrentMatches.Clear();
        gameBoard.CurrentMatches.AddRange(validCascadeMatches);

        foreach (SC_Gem gem in validCascadeMatches)
        {
            if (gem)
            {
                gem.isMatch = true;
            }
        }
    }

    private CascadeMatchValidation ValidateMatchGroup(List<SC_Gem> matchGroup, HashSet<SC_Gem> cascadeGems, HashSet<Vector2Int> cascadePositions)
    {
        bool containsCascadeGem = matchGroup.Any(g => g && cascadeGems.Contains(g));

        if (!containsCascadeGem && cascadePositions.Count > 0)
        {
            containsCascadeGem = matchGroup.Any(g => g && cascadePositions.Contains(g.posIndex));
        }

        if (containsCascadeGem)
        {
            return new CascadeMatchValidation(true);
        }

        bool isBombToBombMatch = matchGroup.Count > 0 &&
                                 matchGroup.All(g => g && g.type == GlobalEnums.GemType.bomb);

        if (isBombToBombMatch)
        {
            bool hasCascadeBomb = matchGroup.Any(g => g && cascadeGems.Contains(g));
            if (!hasCascadeBomb && cascadePositions.Count > 0)
            {
                hasCascadeBomb = matchGroup.Any(g => g && cascadePositions.Contains(g.posIndex));
            }

            bool isAdjacentToCascade = matchGroup.Any(g => IsAdjacentToAnyCascadeGem(g, cascadeGems, cascadePositions));

            if (hasCascadeBomb || isAdjacentToCascade)
            {
                return new CascadeMatchValidation(true);
            }
        }

        return new CascadeMatchValidation(false);
    }

    private bool IsAdjacentToAnyCascadeGem(SC_Gem gem, HashSet<SC_Gem> cascadeGems, HashSet<Vector2Int> cascadePositions)
    {
        if (!gem)
            return false;

        Vector2Int gemPos = gem.posIndex;

        foreach (Vector2Int dir in NEIGHBOR_DIRECTIONS)
        {
            Vector2Int neighborPos = gemPos + dir;
            SC_Gem neighbor = gameBoard.GetGem(neighborPos.x, neighborPos.y);

            if (neighbor && (cascadeGems.Contains(neighbor) || cascadePositions.Contains(neighborPos)))
            {
                return true;
            }
        }

        return false;
    }

    private void AddMatchGroupToValid(HashSet<SC_Gem> validMatchGems, HashSet<SC_Gem> processedGems,
        List<SC_Gem> matchGroup)
    {
        foreach (SC_Gem groupGem in matchGroup)
        {
            validMatchGems.Add(groupGem);
            processedGems.Add(groupGem);
        }
    }

    private void MarkMatchGroupAsProcessed(HashSet<SC_Gem> processedGems, List<SC_Gem> matchGroup)
    {
        foreach (SC_Gem groupGem in matchGroup)
        {
            processedGems.Add(groupGem);
        }
    }

    private struct CascadeMatchValidation
    {
        public bool IsValid { get; }

        public CascadeMatchValidation(bool isValid)
        {
            IsValid = isValid;
        }
    }

    private List<SC_Gem> BuildMatchGroupFromGem(SC_Gem startGem, List<SC_Gem> allMatches)
    {
        if (!startGem)
            return new List<SC_Gem>();

        HashSet<SC_Gem> matchGroup = new HashSet<SC_Gem> { startGem };
        bool isBombMatch = startGem.type == GlobalEnums.GemType.bomb;
        GlobalEnums.GemType gemType = isBombMatch ? GlobalEnums.GemType.bomb : GetGemTypeForMatch(startGem);

        Queue<SC_Gem> toProcess = new Queue<SC_Gem>();
        toProcess.Enqueue(startGem);

        while (toProcess.Count > 0)
        {
            SC_Gem currentGem = toProcess.Dequeue();
            ProcessNeighborsForMatchGroup(currentGem, allMatches, matchGroup, toProcess, isBombMatch, gemType);
        }

        return matchGroup.ToList();
    }

    private void ProcessNeighborsForMatchGroup(SC_Gem currentGem, List<SC_Gem> allMatches,
        HashSet<SC_Gem> matchGroup, Queue<SC_Gem> toProcess, bool isBombMatch, GlobalEnums.GemType gemType)
    {
        Vector2Int currentPos = currentGem.posIndex;

        foreach (Vector2Int dir in NEIGHBOR_DIRECTIONS)
        {
            Vector2Int neighborPos = currentPos + dir;
            SC_Gem neighbor = gameBoard.GetGem(neighborPos.x, neighborPos.y);

            if (ShouldIncludeNeighborInMatchGroup(neighbor, allMatches, matchGroup, isBombMatch, gemType))
            {
                matchGroup.Add(neighbor);
                toProcess.Enqueue(neighbor);
            }
        }
    }

    private bool ShouldIncludeNeighborInMatchGroup(SC_Gem neighbor, List<SC_Gem> allMatches,
        HashSet<SC_Gem> matchGroup, bool isBombMatch, GlobalEnums.GemType gemType)
    {
        if (!neighbor || !allMatches.Contains(neighbor) || matchGroup.Contains(neighbor))
            return false;

        return isBombMatch
            ? neighbor.type == GlobalEnums.GemType.bomb
            : GetGemTypeForMatch(neighbor) == gemType;
    }

    private GlobalEnums.GemType GetGemTypeForMatch(SC_Gem gem)
    {
        if (!gem)
            return GlobalEnums.GemType.blue;

        if (bombHandler == null)
            return gem.type == GlobalEnums.GemType.bomb ? gem.GemColor : gem.type;

        return bombHandler.GetGemColorForMatch(gem);
    }
}
