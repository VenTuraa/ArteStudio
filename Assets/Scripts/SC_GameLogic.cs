using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SC_GameLogic : MonoBehaviour
{
    private Dictionary<string, GameObject> unityObjects;
    private int score = 0;
    private float displayScore = 0;
    private GameBoard gameBoard;
    private GlobalEnums.GameState currentState = GlobalEnums.GameState.move;
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

        SC_Gem _gem = Instantiate(_GemToSpawn, new Vector3(_Position.x, _Position.y + SC_GameVariables.Instance.dropHeight, 0f), Quaternion.identity);
        _gem.transform.SetParent(unityObjects["GemsHolder"].transform);
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

        int nullCounter = 0;
        for (int x = 0; x < gameBoard.Width; x++)
        {
            for (int y = 0; y < gameBoard.Height; y++)
            {
                SC_Gem _curGem = gameBoard.GetGem(x, y);
                if (_curGem == null)
                {
                    nullCounter++;
                }
                else if (nullCounter > 0)
                {
                    _curGem.posIndex.y -= nullCounter;
                    SetGem(x, y - nullCounter, _curGem);
                    SetGem(x, y, null);
                }
            }
            nullCounter = 0;
        }

        StartCoroutine(FilledBoardCo());
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

            Destroy(_curGem.gameObject);
            SetGem(_Pos.x,_Pos.y, null);
        }
    }

    private IEnumerator FilledBoardCo()
    {
        yield return new WaitForSeconds(0.5f);
        RefillBoard();
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
    private void RefillBoard()
    {
        for (int x = 0; x < gameBoard.Width; x++)
        {
            for (int y = 0; y < gameBoard.Height; y++)
            {
                SC_Gem _curGem = gameBoard.GetGem(x,y);
                if (_curGem == null)
                {
                    int gemToUse = Random.Range(0, SC_GameVariables.Instance.gems.Length);
                    SpawnGem(new Vector2Int(x, y), SC_GameVariables.Instance.gems[gemToUse]);
                }
            }
        }
        CheckMisplacedGems();
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
            Destroy(g.gameObject);
    }
    public void FindAllMatches()
    {
        gameBoard.FindAllMatches();
    }

    #endregion
}
