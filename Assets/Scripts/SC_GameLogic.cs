using TMPro;
using UnityEngine;
using Zenject;

public class SC_GameLogic : MonoBehaviour
{
    [SerializeField] private Transform poolParent;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Transform gemsHolder;

    [Inject] private SC_GameVariables gameVariables;
    [Inject] private IScoreService scoreService;
    [Inject] private IGameStateService gameStateService;
    [Inject] private GameBoard.Factory gameBoardFactory;
    [Inject] private GemPool.Factory gemPoolFactory;
    [Inject] private DiContainer container;

    private IGameBoard gameBoard;
    private IGemSpawnerService gemSpawner;
    private IMatchHandlerService matchHandler;
    private ICascadeService cascadeService;
    private IBombHandler bombHandler;
    private GemPool gemPool;

    private float displayScore;

    public GlobalEnums.GameState CurrentState => gameStateService.CurrentState;

    #region MonoBehaviour

    [Inject]
    private void Construct()
    {
        InitializeServices();
    }

    private void Start()
    {
        StartGame();
    }

    private void Update()
    {
        if (gameBoard == null || scoreService == null)
            return;

        if (gameBoard is GameBoard board)
        {
            board.Score = scoreService.Score;
        }

        displayScore = Mathf.Lerp(displayScore, scoreService.Score, gameVariables.scoreSpeed * Time.deltaTime);

        scoreText.text = displayScore.ToString("0");
    }

    #endregion

    #region Initialization

    private void InitializeServices()
    {
        gameBoard = gameBoardFactory.Create(7, 7);
        container.Rebind<IGameBoard>().FromInstance(gameBoard).AsSingle();

        gemPool = gemPoolFactory.Create(poolParent != null ? poolParent : gemsHolder, 50);
        container.Rebind<GemPool>().FromInstance(gemPool).AsSingle();

        gemPool.WarmPool(gameVariables.gems, 5);
        if (gameVariables.bomb)
        {
            SC_Gem[] bombArray = { gameVariables.bomb };
            gemPool.WarmPool(bombArray, 2);
        }

        gemSpawner = container.Resolve<IGemSpawnerService>();
        bombHandler = container.Resolve<IBombHandler>();
        cascadeService = container.Resolve<ICascadeService>();
        matchHandler = container.Resolve<IMatchHandlerService>();

        if (bombHandler is BombLogicService bombService)
        {
            bombService.SetCascadeService(cascadeService);
        }

        if (cascadeService is CascadeService cascade)
        {
            cascade.SetMatchHandler(matchHandler);
            cascade.SetGameLogic(this);
        }

        if (matchHandler is MatchHandlerService matchService)
        {
            matchService.SetBombHandler(bombHandler);
            matchService.SetCascadeService(cascadeService);
        }

        if (gemSpawner is GemSpawnerService spawner)
        {
            spawner.SetGameLogic(this);
        }

        if (gameBoard is GameBoard gameBoardInstance)
        {
            gameBoardInstance.SetBombHandler(bombHandler);
        }

        gemSpawner.InitializeBoard(7, 7);
    }

    private void StartGame()
    {
        scoreService.Reset();
        scoreText.text = "0";
    }

    #endregion

    #region Public API for Gems

    public void SetGem(int x, int y, SC_Gem gem)
    {
        gameBoard.SetGem(x, y, gem);
    }

    public SC_Gem GetGem(int x, int y)
    {
        return gameBoard.GetGem(x, y);
    }

    public void SetState(GlobalEnums.GameState state)
    {
        gameStateService.SetState(state);
    }

    public void FindAllMatches()
    {
        gameBoard.FindAllMatches();
    }

    public void DestroyMatches()
    {
        matchHandler.DestroyMatches();
    }

    #endregion
}