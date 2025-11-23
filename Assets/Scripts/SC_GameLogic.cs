using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
    public GlobalEnums.GameState CurrentState { get { return currentState; } }

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
        if (Random.Range(0, 100f) < SC_GameVariables.Instance.bombChance)
            _GemToSpawn = SC_GameVariables.Instance.bomb;

        Vector3 spawnPosition = new Vector3(_Position.x, _Position.y + SC_GameVariables.Instance.dropHeight, 0f);
        SC_Gem _gem = gemPool.GetGem(_GemToSpawn, spawnPosition, unityObjects["GemsHolder"].transform);
        _gem.name = "Gem - " + _Position.x + ", " + _Position.y;
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
        for (int i = 0; i < gameBoard.CurrentMatches.Count; i++)
            if (gameBoard.CurrentMatches[i] != null)
            {
                ScoreCheck(gameBoard.CurrentMatches[i]);
                DestroyMatchedGemsAt(gameBoard.CurrentMatches[i].posIndex);
            }

        StartCoroutine(DecreaseRowCo());
    }
    private IEnumerator DecreaseRowCo()
    {
        yield return new WaitForSeconds(.2f);

        // Process each column separately for cascading effect
        for (int x = 0; x < gameBoard.Width; x++)
        {
            yield return StartCoroutine(CascadeColumnCo(x));
        }

        StartCoroutine(FilledBoardCo());
    }

    /// <summary>
    /// Cascades gems in a single column one by one from bottom to top.
    /// This ensures gems drop individually rather than as a group.
    /// Also spawns new gems at the top that cascade down with existing gems.
    /// </summary>
    private IEnumerator CascadeColumnCo(int column)
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
            yield return new WaitForSeconds(0.1f);
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
        if (_curGem != null)
        {
            Instantiate(_curGem.destroyEffect, new Vector2(_Pos.x, _Pos.y), Quaternion.identity);

            SetGem(_Pos.x,_Pos.y, null);
            gemPool.ReturnToPool(_curGem);
        }
    }

    private IEnumerator FilledBoardCo()
    {
        yield return new WaitForSeconds(0.5f);
        // RefillBoard is no longer needed - gems are spawned during cascading
        // But we still need to check for misplaced gems
        CheckMisplacedGems();
        yield return new WaitForSeconds(0.5f);
        gameBoard.FindAllMatches();
        if (gameBoard.CurrentMatches.Count > 0)
        {
            yield return new WaitForSeconds(0.5f);
            DestroyMatches();
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
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
