using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class SC_GameLogic : MonoBehaviour
{
    [SerializeField] private Transform poolParent;
    
    private Dictionary<string, GameObject> unityObjects;
    private int score = 0;
    private float displayScore = 0;
    private GameBoard gameBoard;
    private GlobalEnums.GameState currentState = GlobalEnums.GameState.move;
    private IMatchPreventionStrategy matchPrevention;
    private GemPool gemPool;
    private BombLogicService bombLogicService;
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
            unityObjects.Add(g.name,g);

        gameBoard = new GameBoard(7, 7);
        matchPrevention = new GemMatchPrevention();
        
        // Initialize bomb logic service and inject into game board
        bombLogicService = new BombLogicService(gameBoard);
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
                Vector2 _pos = new Vector2(x, y);
                GameObject _bgTile = Instantiate(SC_GameVariables.Instance.bgTilePrefabs, _pos, Quaternion.identity);
                _bgTile.transform.SetParent(unityObjects["GemsHolder"].transform);
                _bgTile.name = "BG Tile - " + x + ", " + y;

                int _gemToUse = Random.Range(0, SC_GameVariables.Instance.gems.Length);

                int iterations = 0;
                while (gameBoard.MatchesAt(new Vector2Int(x, y), SC_GameVariables.Instance.gems[_gemToUse]) && iterations < 100)
                {
                    _gemToUse = Random.Range(0, SC_GameVariables.Instance.gems.Length);
                    iterations++;
                }
                SpawnGem(new Vector2Int(x, y), SC_GameVariables.Instance.gems[_gemToUse]);
            }
    }
    public void StartGame()
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
        
        gameBoard.SetGem(_Position.x,_Position.y, _gem);
        _gem.SetupGem(this,_Position);
    }
    public void SetGem(int _X,int _Y, SC_Gem _Gem)
    {
        gameBoard.SetGem(_X,_Y, _Gem);
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
        List<SC_Gem> bombsToExplode = new List<SC_Gem>();
        List<SC_Gem> regularMatches = new List<SC_Gem>();
        
        // Separate bombs from regular gems
        for (int i = 0; i < gameBoard.CurrentMatches.Count; i++)
        {
            SC_Gem gem = gameBoard.CurrentMatches[i];
            if (gem != null && gem.isMatch)
            {
                if (gem.type == GlobalEnums.GemType.bomb)
                {
                    bombsToExplode.Add(gem);
                }
                else
                {
                    regularMatches.Add(gem);
                }
            }
        }
        
        // Destroy regular matched gems first
        foreach (SC_Gem gem in regularMatches)
        {
            if (gem != null && gem.type != GlobalEnums.GemType.bomb && gem.isMatch)
            {
                ScoreCheck(gem);
                DestroyMatchedGemsAt(gem.posIndex);
            }
        }
        
        // Create new bombs from 4+ matches
        if (gameBoard.BombsToCreate.Count > 0)
        {
            foreach (var bombInfo in gameBoard.BombsToCreate)
            {
                CreateBombAt(bombInfo.position, bombInfo.gemType);
            }
        }
        
        // Handle bomb explosions with delays
        if (bombsToExplode.Count > 0)
        {
            HandleBombExplosions(bombsToExplode).Forget();
        }
        else
        {
            DecreaseRowCo().Forget();
        }
    }
    
    private void CreateBombAt(Vector2Int position, GlobalEnums.GemType gemType)
    {
        SC_Gem existingGem = gameBoard.GetGem(position.x, position.y);
        if (existingGem != null && existingGem.isMatch)
        {
            DestroyMatchedGemsAt(position);
        }
        
        SC_Gem bombPrefab = SC_GameVariables.Instance.bomb;
        if (bombPrefab != null)
        {
            Vector3 spawnPosition = new Vector3(position.x, position.y, 0f);
            SC_Gem bomb = gemPool.GetGem(bombPrefab, spawnPosition, unityObjects["GemsHolder"].transform);
            bomb.name = "Bomb - " + position.x + ", " + position.y;
            bomb.type = GlobalEnums.GemType.bomb;
            bomb.GemColor = gemType; // Set the bomb's color for matching logic
            bomb.isMatch = false;
            
            // Apply color sprite from the matching gem type
            bombLogicService.ApplyBombColor(bomb, gemType);
            
            gameBoard.SetGem(position.x, position.y, bomb);
            bomb.SetupGem(this, position);
        }
    }
    
    private async UniTask DecreaseRowCo()
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.2f));

        // Process each column separately for cascading effect
        for (int x = 0; x < gameBoard.Width; x++)
        {
            await CascadeColumnCo(x);
        }

        FilledBoardCo().Forget();
    }

    /// <summary>
    /// Cascades gems in a single column one by one from bottom to top.
    /// This ensures gems drop individually rather than as a group.
    /// Also spawns new gems at the top that cascade down with existing gems.
    /// </summary>
    private async UniTask CascadeColumnCo(int column)
    {
        // First pass: Calculate how far each gem needs to drop
        List<GemDropInfo> dropQueue = new List<GemDropInfo>();
        int nullCounter = 0;

        // Process from bottom to top
        for (int y = 0; y < gameBoard.Height; y++)
        {
            SC_Gem currentGem = gameBoard.GetGem(column, y);
            if (currentGem == null)
            {
                nullCounter++;
            }
            else if (nullCounter > 0)
            {
                // This gem needs to drop
                int targetY = y - nullCounter;
                dropQueue.Add(new GemDropInfo
                {
                    gem = currentGem,
                    sourceY = y,
                    targetY = targetY
                });
            }
        }

        // Spawn new gems at the top for empty slots and add them to the drop queue
        for (int i = 0; i < nullCounter; i++)
        {
            int targetY = gameBoard.Height - nullCounter + i; // Target position after cascade
            int spawnY = gameBoard.Height + i; // Spawn above the board (for visual cascading)

            // Use match prevention to avoid unintended matches
            SC_Gem safeGem = matchPrevention.GetSafeGemType(
                gameBoard,
                new Vector2Int(column, targetY), // Check at target position, not spawn position
                SC_GameVariables.Instance.gems
            );

            // Fallback to random if prevention returns null
            if (!safeGem)
            {
                safeGem = SC_GameVariables.Instance.gems[Random.Range(0, SC_GameVariables.Instance.gems.Length)];
            }

            // Spawn gem above the board - it will cascade down visually
            Vector3 spawnPosition = new Vector3(column, spawnY + SC_GameVariables.Instance.dropHeight, 0f);
            SC_Gem newGem = gemPool.GetGem(safeGem, spawnPosition, unityObjects["GemsHolder"].transform);
            newGem.name = "Gem - " + column + ", " + targetY;
            
            // Set GemColor for regular gems (for bomb matching logic)
            // Important: Always set GemColor when getting gem from pool
            if (newGem.type != GlobalEnums.GemType.bomb)
            {
                newGem.GemColor = newGem.type;
            }
            else
            {
                // For bombs, preserve GemColor if it was set, otherwise use default
                if (newGem.GemColor == GlobalEnums.GemType.blue && safeGem.type == GlobalEnums.GemType.bomb)
                {
                    // If bomb was reset, we need to get color from prefab or keep default
                    // This should be handled in CreateBombAt
                }
            }
            
            // Set initial position above board, gem will animate to target position
            newGem.SetupGem(this, new Vector2Int(column, spawnY));

            // Add to drop queue - it will cascade down with other gems
            dropQueue.Add(new GemDropInfo
            {
                gem = newGem,
                sourceY = spawnY,
                targetY = targetY
            });
        }

        // Second pass: Execute drops one by one from bottom to top
        // This creates the cascading visual effect (bottom gems drop first)
        for (int i = 0; i < dropQueue.Count; i++)
        {
            GemDropInfo dropInfo = dropQueue[i];
            
            // Update gem's target position - it will animate there
            dropInfo.gem.posIndex.y = dropInfo.targetY;
            
            // Register gem at target position in gameBoard
            SetGem(column, dropInfo.targetY, dropInfo.gem);
            
            // Clear source position if it's within board bounds (existing gems)
            // Newly spawned gems above the board don't need clearing
            if (dropInfo.sourceY < gameBoard.Height)
            {
                SetGem(column, dropInfo.sourceY, null);
            }

            // Wait for gem to start moving before dropping the next one
            // This creates the cascading effect where gems fall one by one
            await UniTask.Delay(System.TimeSpan.FromSeconds(SC_GameVariables.Instance.cascadeDelay));
        }
    }

    /// <summary>
    /// Helper struct to track gem drop information.
    /// </summary>
    private struct GemDropInfo
    {
        public SC_Gem gem;
        public int sourceY;
        public int targetY;
    }

    public void ScoreCheck(SC_Gem gemToCheck)
    {
        gameBoard.Score += gemToCheck.scoreValue;
    }
    private void DestroyMatchedGemsAt(Vector2Int _Pos)
    {
        SC_Gem _curGem = gameBoard.GetGem(_Pos.x,_Pos.y);
        if (_curGem != null && _curGem.type != GlobalEnums.GemType.bomb) // Don't destroy bombs here
        {
            Instantiate(_curGem.destroyEffect, new Vector2(_Pos.x, _Pos.y), Quaternion.identity);

            SetGem(_Pos.x,_Pos.y, null);
            gemPool.ReturnToPool(_curGem);
        }
    }
    
    private void DestroyBombAt(Vector2Int _Pos)
    {
        SC_Gem _curGem = gameBoard.GetGem(_Pos.x,_Pos.y);
        if (_curGem != null && _curGem.type == GlobalEnums.GemType.bomb)
        {
            if (_curGem.destroyEffect != null)
                Instantiate(_curGem.destroyEffect, new Vector2(_Pos.x, _Pos.y), Quaternion.identity);

            SetGem(_Pos.x,_Pos.y, null);
            gemPool.ReturnToPool(_curGem);
        }
    }
    
    private async UniTask HandleBombExplosions(List<SC_Gem> bombs)
    {
        foreach (SC_Gem bomb in bombs)
        {
            if (bomb == null || !bomb.gameObject.activeInHierarchy)
                continue;
                
            Vector2Int bombPos = bomb.posIndex;
            
            // Ensure the bomb is still at its expected position in the gameBoard
            SC_Gem bombAtPos = gameBoard.GetGem(bombPos.x, bombPos.y);
            if (bombAtPos != bomb)
                continue;
            
            List<Vector2Int> explosionPositions = gameBoard.GetBombExplosionPattern(bombPos);
            
            // Wait before destroying neighbor group
            await UniTask.Delay(System.TimeSpan.FromSeconds(SC_GameVariables.Instance.bombNeighborDestroyDelay));
            
            // Destroy neighbor pieces in explosion pattern
            foreach (Vector2Int pos in explosionPositions)
            {
                SC_Gem gem = gameBoard.GetGem(pos.x, pos.y);
                if (gem != null && gem != bomb && gem.type != GlobalEnums.GemType.bomb) // Don't destroy other bombs prematurely
                {
                    ScoreCheck(gem);
                    DestroyMatchedGemsAt(pos);
                }
            }
            
            // Wait before destroying the bomb itself
            await UniTask.Delay(System.TimeSpan.FromSeconds(SC_GameVariables.Instance.bombDestroyDelay));
            
            // Re-check if the bomb is still there before destroying it
            bombAtPos = gameBoard.GetGem(bombPos.x, bombPos.y);
            if (bombAtPos == bomb)
            {
                ScoreCheck(bomb);
                DestroyBombAt(bombPos);
            }
        }
        
        // Cascading starts only after all bombs are destroyed
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.2f));
        DecreaseRowCo().Forget();
    }

    private async UniTask FilledBoardCo()
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));
        // RefillBoard is no longer needed - gems are spawned during cascading
        // But we still need to check for misplaced gems
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
        List<SC_Gem> foundGems = new List<SC_Gem>();
        foundGems.AddRange(FindObjectsOfType<SC_Gem>());
        for (int x = 0; x < gameBoard.Width; x++)
        {
            for (int y = 0; y < gameBoard.Height; y++)
            {
                SC_Gem _curGem = gameBoard.GetGem(x, y);
                if (foundGems.Contains(_curGem))
                    foundGems.Remove(_curGem);
            }
        }

        foreach (SC_Gem g in foundGems)
            gemPool.ReturnToPool(g);
    }
    public void FindAllMatches()
    {
        gameBoard.FindAllMatches();
    }

    #endregion
}
