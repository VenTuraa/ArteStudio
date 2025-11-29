using UnityEngine;
using Zenject;

public class GameInstaller : MonoInstaller
{
    [SerializeField] private SC_GameVariables gameVariables;

    public override void InstallBindings()
    {
        Container.Bind<SC_GameVariables>().FromInstance(gameVariables).AsSingle();


        Container.BindFactory<int, int, GameBoard, GameBoard.Factory>()
            .FromMethod((container, width, height) => new GameBoard(width, height));

        Container.Bind<IMatchPreventionStrategy>().To<GemMatchPrevention>().AsSingle();

        Container.BindFactory<GameBoard, BombLogicService, BombLogicService.Factory>()
            .FromMethod((container, gameBoard) =>
                new BombLogicService(gameBoard, container.Resolve<SC_GameVariables>()));

        Container.BindFactory<Transform, int, GemPool, GemPool.Factory>()
            .FromMethod((container, poolParent, maxPoolSize) =>
                new GemPool(poolParent, maxPoolSize, container));
    }
}