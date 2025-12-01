using TMPro;
using UnityEngine;
using Zenject;

public class GameInstaller : MonoInstaller
{
    [SerializeField] private SC_GameVariables gameVariables;
    [SerializeField] private Transform gemsHolder;
    [SerializeField] private TextMeshProUGUI scoreText;

    public override void InstallBindings()
    {
        Container.Bind<SC_GameVariables>().FromInstance(gameVariables).AsSingle();

        Container.Bind<Transform>().FromInstance(gemsHolder).AsSingle();

        Container.Bind<IScoreService>().To<ScoreService>().AsSingle();
        Container.Bind<IGameStateService>().To<GameStateService>().AsSingle();
        Container.Bind<IMatchPreventionStrategy>().To<GemMatchPrevention>().AsSingle();
        Container.Bind<IGameConfig>().To<GameConfigService>().AsSingle();

        Container.BindFactory<int, int, GameBoard, GameBoard.Factory>()
            .FromMethod((container, width, height) => new GameBoard(width, height));

        Container.BindFactory<Transform, int, GemPool, GemPool.Factory>()
            .FromMethod((container, poolParent, maxPoolSize) =>
                new GemPool(poolParent, maxPoolSize, container));

        Container.Bind<IGemDestroyer>().To<GemDestroyerService>().AsSingle();
        Container.Bind<IGemSpawnerService>().To<GemSpawnerService>().AsSingle();
        Container.Bind<IBombHandler>().To<BombLogicService>().AsSingle();
        Container.Bind<ICascadeService>().To<CascadeService>().AsSingle();
        Container.Bind<IMatchHandlerService>().To<MatchHandlerService>().AsSingle();

        Container.Bind<TextMeshProUGUI>().FromInstance(scoreText).AsSingle();

        Container.Bind<UIUpdateService>().AsSingle();
    }
}