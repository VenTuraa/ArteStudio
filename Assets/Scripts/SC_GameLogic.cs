using TMPro;
using UnityEngine;
using Zenject;

public class SC_GameLogic : MonoBehaviour, IGameCoordinator
{
    [SerializeField] private Transform poolParent;
    [SerializeField] private Transform gemsHolder;

    [Inject] private SC_GameVariables gameVariables;
    [Inject] private IScoreService scoreService;
    [Inject] private IGameStateService gameStateService;
    [Inject] private IGameConfig gameConfig;
    [Inject] private GameBoard.Factory gameBoardFactory;
    [Inject] private GemPool.Factory gemPoolFactory;
    [Inject] private DiContainer container;
    [Inject] private UIUpdateService uiUpdateService;

    private IGameBoard gameBoard;
    private IGemSpawnerService gemSpawner;
    private IMatchHandlerService matchHandler;
    private ICascadeService cascadeService;
    private IBombHandler bombHandler;
    private GemPool gemPool;

    GlobalEnums.GameState IGameCoordinator.CurrentState => gameStateService.CurrentState;
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

    #endregion

    #region Initialization

    private void InitializeServices()
    {
        gameBoard = gameBoardFactory.Create(gameConfig.BoardWidth, gameConfig.BoardHeight);
        container.Rebind<IGameBoard>().FromInstance(gameBoard).AsSingle();

        gemPool = gemPoolFactory.Create(poolParent != null ? poolParent : gemsHolder, gameConfig.PoolSize);
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

        SetupCircularDependencies();

        gemSpawner.InitializeBoard(gameConfig.BoardWidth, gameConfig.BoardHeight);
    }

    private void SetupCircularDependencies()
    {
        bombHandler.SetCascadeService(cascadeService);
        cascadeService.SetMatchHandler(matchHandler);
        cascadeService.SetGameCoordinator(this);
        matchHandler.SetBombHandler(bombHandler);
        matchHandler.SetCascadeService(cascadeService);
        gemSpawner.SetGameCoordinator(this);
        gameBoard.SetBombHandler(bombHandler);
    }

    private void StartGame()
    {
        scoreService.Reset();
        uiUpdateService.Reset();
    }

    #endregion

    #region IGameCoordinator Implementation

    void IGameCoordinator.SetGem(int x, int y, SC_Gem gem)
    {
        gameBoard.SetGem(x, y, gem);
    }

    SC_Gem IGameCoordinator.GetGem(int x, int y)
    {
        return gameBoard.GetGem(x, y);
    }

    void IGameCoordinator.SetState(GlobalEnums.GameState state)
    {
        gameStateService.SetState(state);
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