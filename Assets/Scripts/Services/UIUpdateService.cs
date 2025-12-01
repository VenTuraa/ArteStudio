using TMPro;
using UnityEngine;
using Zenject;
using Cysharp.Threading.Tasks;
using System.Threading;

public class UIUpdateService
{
    private readonly IScoreService scoreService;
    private readonly SC_GameVariables gameVariables;
    private readonly TextMeshProUGUI scoreText;
    private float displayScore;
    private int targetScore;
    private CancellationTokenSource updateCancellation;

    [Inject]
    public UIUpdateService(
        IScoreService scoreService,
        SC_GameVariables gameVariables,
        TextMeshProUGUI scoreText = null)
    {
        this.scoreService = scoreService;
        this.gameVariables = gameVariables;
        this.scoreText = scoreText;
        targetScore = 0;
        displayScore = 0;

        if (scoreService != null)
            scoreService.OnScoreChanged += OnScoreChanged;
    }

    private void OnScoreChanged(int newScore)
    {
        if (!scoreText)
            return;

        targetScore = newScore;

        updateCancellation?.Cancel();

        updateCancellation = new CancellationTokenSource();
        AnimateScore(updateCancellation.Token).Forget();
    }

    private async UniTaskVoid AnimateScore(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (Mathf.Abs(displayScore - targetScore) < 0.1f)
            {
                displayScore = targetScore;
                if (scoreText)
                    scoreText.text = displayScore.ToString("0");
                break;
            }

            displayScore = Mathf.Lerp(displayScore, targetScore, gameVariables.scoreSpeed * Time.deltaTime);
            if (scoreText)
                scoreText.text = displayScore.ToString("0");

            await UniTask.Yield(cancellationToken);
        }
    }

    public void Reset()
    {
        if (updateCancellation != null)
        {
            updateCancellation.Cancel();
            updateCancellation.Dispose();
            updateCancellation = null;
        }

        displayScore = 0;
        targetScore = 0;
        if (scoreText)
        {
            scoreText.text = "0";
        }
    }
}


