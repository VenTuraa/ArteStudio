using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Random = UnityEngine.Random;

public class SC_GameLogic : MonoBehaviour
{
    private const int MAX_MATCH_PREVENTION_ITERATIONS = 100;

    [SerializeField] private Transform poolParent;

    private Dictionary<string, GameObject> unityObjects;
    private int score = 0;
    private float displayScore;
    private GameBoard gameBoard;
    private GlobalEnums.GameState currentState = GlobalEnums.GameState.move;
    private IMatchPreventionStrategy matchPrevention;
    private GemPool gemPool;
    private BombLogicService bombLogicService;
    private HashSet<SC_Gem> explodingBombs = new();
    private bool isProcessingBombExplosions;
    public GlobalEnums.GameState CurrentState => currentState;

    #region MonoBehaviour

    private void Awake()
    {
        Init();
    }

    private void Start()
    {
        StartGame();
    }

    private void Update()
    {
        displayScore = Mathf.Lerp(displayScore, gameBoard.Score, SC_GameVariables.Instance.scoreSpeed * Time.deltaTime);
        unityObjects["Txt_Score"].GetComponent<TMPro.TextMeshProUGUI>().text = displayScore.ToString("0");
    }

    #endregion

    #region Logic

    private void Init()
    {
        unityObjects = new Dictionary<string, GameObject>();
        GameObject[] _obj = GameObject.FindGameObjectsWithTag("UnityObject");
        foreach (GameObject g in _obj)
            unityObjects.Add(g.name, g);

        gameBoard = new GameBoard(7, 7);
        matchPrevention = new GemMatchPrevention();

        // Initialize bomb logic service and inject into game board
        bombLogicService = new BombLogicService(gameBoard);
        bombLogicService.SetCallbacks(ScoreCheck, DestroyGem, OnBombExplosionsComplete, CreateBombInstance);
        gameBoard.SetBombLogicService(bombLogicService);

        gemPool = new GemPool(poolParent.transform, 50);

        gemPool.WarmPool(SC_GameVariables.Instance.gems, 5);
        if (SC_GameVariables.Instance.bomb != null)
        {
            SC_Gem[] bombArray = { SC_GameVariables.Instance.bomb };
            gemPool.WarmPool(bombArray, 2);
        }

        Setup();
    }

    private void Setup()
    {
        for (int x = 0; x < gameBoard.Width; x++)
        for (int y = 0; y < gameBoard.Height; y++)
        {
            CreateBackgroundTile(x, y);
            SC_Gem safeGem = GetInitialGemForPosition(x, y, MAX_MATCH_PREVENTION_ITERATIONS);
            SpawnGem(new Vector2Int(x, y), safeGem);
        }
    }

    private void CreateBackgroundTile(int x, int y)
    {
        Vector2 position = new Vector2(x, y);
        GameObject bgTile = Instantiate(SC_GameVariables.Instance.bgTilePrefabs, position, Quaternion.identity);
        bgTile.transform.SetParent(unityObjects["GemsHolder"].transform);
        bgTile.name = "BG Tile - " + x + ", " + y;
    }

    private SC_Gem GetInitialGemForPosition(int x, int y, int maxIterations)
    {
        int gemIndex = Random.Range(0, SC_GameVariables.Instance.gems.Length);
        int iterations = 0;

        while (gameBoard.MatchesAt(new Vector2Int(x, y), SC_GameVariables.Instance.gems[gemIndex]) &&
               iterations < maxIterations)
        {
            gemIndex = Random.Range(0, SC_GameVariables.Instance.gems.Length);
            iterations++;
        }

        return SC_GameVariables.Instance.gems[gemIndex];
    }

    private void StartGame()
    {
        unityObjects["Txt_Score"].GetComponent<TextMeshProUGUI>().text = score.ToString("0");
    }

    private void SpawnGem(Vector2Int _Position, SC_Gem _GemToSpawn)
    {
        // Bombs are only created through match 4+ logic, not randomly
        Vector3 spawnPosition = new Vector3(_Position.x, _Position.y + SC_GameVariables.Instance.dropHeight, 0f);
        SC_Gem _gem = gemPool.GetGem(_GemToSpawn, spawnPosition, unityObjects["GemsHolder"].transform);
        _gem.name = "Gem - " + _Position.x + ", " + _Position.y;

        // Set GemColor for regular gems (for bomb matching logic)
        if (_gem.type != GlobalEnums.GemType.bomb)
        {
            _gem.GemColor = _gem.type;
        }

        gameBoard.SetGem(_Position.x, _Position.y, _gem);
        _gem.SetupGem(this, _Position);
    }

    public void SetGem(int _X, int _Y, SC_Gem _Gem)
    {
        gameBoard.SetGem(_X, _Y, _Gem);
    }

    public SC_Gem GetGem(int _X, int _Y)
    {
        return gameBoard.GetGem(_X, _Y);
    }

    public void SetState(GlobalEnums.GameState _CurrentState)
    {
        currentState = _CurrentState;
    }

    public void DestroyMatches()
    {
        var (bombsToExplode, regularMatches) = SeparateBombsFromRegularMatches();

        DestroyRegularMatches(regularMatches);

        HashSet<Vector2Int> newBombPositions = bombLogicService.CreateBombsFromMatches();
        bombLogicService.RemoveNewBombsFromMatches(newBombPositions, gameBoard.CurrentMatches);

        if (bombsToExplode.Count > 0)
        {
            isProcessingBombExplosions = true;
            bombLogicService.HandleBombExplosions(bombsToExplode, explodingBombs).Forget();
        }
        else
        {
            DecreaseRowCo().Forget();
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
                ScoreCheck(gem);
                DestroyGem(gem.posIndex, g => g.type != GlobalEnums.GemType.bomb);
            }
        }
    }

    private SC_Gem CreateBombInstance(Vector2Int position, GlobalEnums.GemType gemType)
    {
        SC_Gem bombPrefab = SC_GameVariables.Instance.bomb;
        if (!bombPrefab)
            return null;

        Vector3 spawnPosition = new Vector3(position.x, position.y, 0f);
        SC_Gem bomb = gemPool.GetGem(bombPrefab, spawnPosition, unityObjects["GemsHolder"].transform);
        bomb.name = "Bomb - " + position.x + ", " + position.y;
        bomb.SetupGem(this, position);

        return bomb;
    }

    private async UniTask DecreaseRowCo()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(0.2f));

        // Process each column separately for cascading effect
        for (int x = 0; x < gameBoard.Width; x++)
        {
            await CascadeColumn(x);
        }

        FilledBoardCo().Forget();
    }


    private async UniTask CascadeColumn(int column)
    {
        List<GemDropInfo> dropQueue = BuildDropQueueForColumn(column);
        SpawnNewGemsForColumn(column, dropQueue);
        await ExecuteStaggeredDrop(column, dropQueue);
    }

    private List<GemDropInfo> BuildDropQueueForColumn(int column)
    {
        List<GemDropInfo> dropQueue = new List<GemDropInfo>();
        int nullCounter = 0;

        for (int y = 0; y < gameBoard.Height; y++)
        {
            SC_Gem currentGem = gameBoard.GetGem(column, y);
            if (!currentGem)
            {
                nullCounter++;
            }
            else if (nullCounter > 0)
            {
                int targetY = y - nullCounter;
                dropQueue.Add(new GemDropInfo
                {
                    gem = currentGem,
                    sourceY = y,
                    targetY = targetY
                });
            }
        }

        return dropQueue;
    }

    private void SpawnNewGemsForColumn(int column, List<GemDropInfo> dropQueue)
    {
        int nullCounter = CountNullsInColumn(column);
        List<GemDropInfo> newGemsQueue = new List<GemDropInfo>();

        for (int i = 0; i < nullCounter; i++)
        {
            int targetY = gameBoard.Height - nullCounter + i;
            int spawnY = gameBoard.Height + i;

            SC_Gem safeGem = GetSafeGemForPosition(column, targetY, dropQueue, newGemsQueue);
            SC_Gem newGem = SpawnGemAtPosition(column, spawnY, safeGem);

            if (newGem.type != GlobalEnums.GemType.bomb)
            {
                newGem.GemColor = newGem.type;
            }

            newGem.SetupGem(this, new Vector2Int(column, spawnY));

            GemDropInfo newGemInfo = new GemDropInfo
            {
                gem = newGem,
                sourceY = spawnY,
                targetY = targetY
            };

            newGemsQueue.Add(newGemInfo);
            dropQueue.Add(newGemInfo);
        }

        dropQueue.Sort((a, b) => a.targetY.CompareTo(b.targetY));
    }

    private int CountNullsInColumn(int column)
    {
        int count = 0;
        for (int y = 0; y < gameBoard.Height; y++)
        {
            if (!gameBoard.GetGem(column, y))
                count++;
        }

        return count;
    }

    private SC_Gem GetSafeGemForPosition(int column, int targetY, List<GemDropInfo> existingDropQueue,
        List<GemDropInfo> newGemsQueue)
    {
        GameBoard simulatedBoard = CreateSimulatedBoardState(column, targetY, existingDropQueue, newGemsQueue);

        SC_Gem safeGem = matchPrevention.GetSafeGemType(
            simulatedBoard,
            new Vector2Int(column, targetY),
            SC_GameVariables.Instance.gems
        );

        if (!safeGem)
        {
            safeGem = SC_GameVariables.Instance.gems[Random.Range(0, SC_GameVariables.Instance.gems.Length)];
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

    private SC_Gem SpawnGemAtPosition(int column, int spawnY, SC_Gem gemPrefab)
    {
        Vector3 spawnPosition = new Vector3(column, spawnY + SC_GameVariables.Instance.dropHeight, 0f);
        SC_Gem newGem = gemPool.GetGem(gemPrefab, spawnPosition, unityObjects["GemsHolder"].transform);
        newGem.name = "Gem - " + column + ", " + spawnY;
        return newGem;
    }

    private async UniTask ExecuteStaggeredDrop(int column, List<GemDropInfo> dropQueue)
    {
        const float STAGGER_THRESHOLD = 0.02f;

        for (int i = 0; i < dropQueue.Count; i++)
        {
            GemDropInfo dropInfo = dropQueue[i];
            dropInfo.gem.posIndex.y = dropInfo.targetY;
            SetGem(column, dropInfo.targetY, dropInfo.gem);

            if (dropInfo.sourceY < gameBoard.Height)
            {
                SetGem(column, dropInfo.sourceY, null);
            }

            if (i < dropQueue.Count - 1)
            {
                await WaitForGemChainTrigger(dropInfo.gem, dropInfo.sourceY, STAGGER_THRESHOLD);
            }
        }
    }

    private struct GemDropInfo
    {
        public SC_Gem gem;
        public int sourceY;
        public int targetY;
    }

    private async UniTask WaitForGemChainTrigger(SC_Gem gem, float startY, float threshold)
    {
        if (!gem)
            return;

        const float MAX_WAIT_TIME = 1.0f;
        const float CASCADE_DELAY_MULTIPLIER = 0.5f;
        float startTime = Time.time;

        while (Time.time - startTime < MAX_WAIT_TIME)
        {
            if (!gem)
                break;

            float currentY = gem.transform.position.y;
            float distanceMoved = startY - currentY;

            if (distanceMoved >= threshold)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(SC_GameVariables.Instance.cascadeDelay * CASCADE_DELAY_MULTIPLIER));
                return;
            }

            await UniTask.Yield();
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(SC_GameVariables.Instance.cascadeDelay));
    }

    private void ScoreCheck(SC_Gem gemToCheck)
    {
        gameBoard.Score += gemToCheck.scoreValue;
    }

    private void DestroyGemAt(Vector2Int pos)
    {
        SC_Gem curGem = gameBoard.GetGem(pos.x, pos.y);
        if (!curGem)
            return;

        if (curGem.destroyEffect)
            Instantiate(curGem.destroyEffect, new Vector2(pos.x, pos.y), Quaternion.identity);

        SetGem(pos.x, pos.y, null);
        gameBoard.ActiveGems.Remove(curGem);
        gemPool.ReturnToPool(curGem);
    }

    private void DestroyGem(Vector2Int pos, Func<SC_Gem, bool> condition)
    {
        SC_Gem curGem = gameBoard.GetGem(pos.x, pos.y);
        if (curGem && condition(curGem))
        {
            DestroyGemAt(pos);
        }
    }

    private void OnBombExplosionsComplete()
    {
        isProcessingBombExplosions = false;
        DecreaseRowCo().Forget();
    }

    private async UniTask FilledBoardCo()
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));

        CheckMisplacedGems();
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));
        gameBoard.FindAllMatches();
        if (gameBoard.CurrentMatches.Count > 0)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));
            DestroyMatches();
        }
        else
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));
            currentState = GlobalEnums.GameState.move;
        }
    }

    private void CheckMisplacedGems()
    {
        // Get all active gems from GameBoard
        HashSet<SC_Gem> foundGems = new HashSet<SC_Gem>(gameBoard.ActiveGems);

        // Remove gems that are properly placed on the board
        for (int x = 0; x < gameBoard.Width; x++)
        {
            for (int y = 0; y < gameBoard.Height; y++)
            {
                SC_Gem curGem = gameBoard.GetGem(x, y);
                if (curGem)
                    foundGems.Remove(curGem);
            }
        }

        // Return misplaced gems to pool
        foreach (SC_Gem g in foundGems)
        {
            gameBoard.ActiveGems.Remove(g);
            gemPool.ReturnToPool(g);
        }
    }

    public void FindAllMatches()
    {
        gameBoard.FindAllMatches();
    }

    #endregion
}