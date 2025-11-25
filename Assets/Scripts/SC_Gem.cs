using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class SC_Gem : MonoBehaviour
{
    [HideInInspector]
    public Vector2Int posIndex;

    [SerializeField] private SpriteRenderer spriteRenderer;
    
    private Vector2 firstTouchPosition;
    private Vector2 finalTouchPosition;
    private bool mousePressed;
    private float swipeAngle = 0;
    private SC_Gem otherGem;

    public GlobalEnums.GemType type;
    public GlobalEnums.GemType GemColor = GlobalEnums.GemType.blue; // Color of the bomb (for matching logic)
    public bool isMatch = false;
    private Vector2Int previousPos;
    public GameObject destroyEffect;
    public int scoreValue = 10;
    public SpriteRenderer Sprite => spriteRenderer;
    public int blastSize = 1;
    private SC_GameLogic scGameLogic;

    private Vector3 velocity = Vector3.zero;
    
    void Update()
    {
        if (Vector2.Distance(transform.position, posIndex) > 0.01f)
        {
            Vector3 targetPosition = new Vector3(posIndex.x, posIndex.y, 0);
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, 0.12f, SC_GameVariables.Instance.gemSpeed * 1.5f, Time.deltaTime);
        }
        else
        {
            transform.position = new Vector3(posIndex.x, posIndex.y, 0);
            velocity = Vector3.zero;
            if (posIndex.x >= 0 && posIndex.x < SC_GameVariables.Instance.rowsSize &&
                posIndex.y >= 0 && posIndex.y < SC_GameVariables.Instance.colsSize)
            {
                scGameLogic.SetGem(posIndex.x, posIndex.y, this);
            }
        }
        if (mousePressed && Input.GetMouseButtonUp(0))
        {
            mousePressed = false;
            if (scGameLogic.CurrentState == GlobalEnums.GameState.move)
            {
                finalTouchPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                CalculateAngle();
            }
        }
    }

    public void SetupGem(SC_GameLogic _ScGameLogic,Vector2Int _Position)
    {
        posIndex = _Position;
        scGameLogic = _ScGameLogic;
    }

    private void OnMouseDown()
    {
        if (scGameLogic.CurrentState == GlobalEnums.GameState.move)
        {
            firstTouchPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePressed = true;
        }
    }

    private void CalculateAngle()
    {
        swipeAngle = Mathf.Atan2(finalTouchPosition.y - firstTouchPosition.y, finalTouchPosition.x - firstTouchPosition.x);
        swipeAngle = swipeAngle * 180 / Mathf.PI;

        if (Vector3.Distance(firstTouchPosition, finalTouchPosition) > .5f)
            MovePieces();
    }

    private void MovePieces()
    {
        previousPos = posIndex;

        if (swipeAngle < 45 && swipeAngle > -45 && posIndex.x < SC_GameVariables.Instance.rowsSize - 1)
        {
            otherGem = scGameLogic.GetGem(posIndex.x + 1, posIndex.y);
            otherGem.posIndex.x--;
            posIndex.x++;

        }
        else if (swipeAngle > 45 && swipeAngle <= 135 && posIndex.y < SC_GameVariables.Instance.colsSize - 1)
        {
            otherGem = scGameLogic.GetGem(posIndex.x, posIndex.y + 1);
            otherGem.posIndex.y--;
            posIndex.y++;
        }
        else if (swipeAngle < -45 && swipeAngle >= -135 && posIndex.y > 0)
        {
            otherGem = scGameLogic.GetGem(posIndex.x, posIndex.y - 1);
            otherGem.posIndex.y++;
            posIndex.y--;
        }
        else if (swipeAngle > 135 || swipeAngle < -135 && posIndex.x > 0)
        {
            otherGem = scGameLogic.GetGem(posIndex.x - 1, posIndex.y);
            otherGem.posIndex.x++;
            posIndex.x--;
        }

        scGameLogic.SetGem(posIndex.x,posIndex.y, this);
        scGameLogic.SetGem(otherGem.posIndex.x, otherGem.posIndex.y, otherGem);

        CheckMoveCo().Forget();
    }

    private async UniTask CheckMoveCo()
    {
        scGameLogic.SetState(GlobalEnums.GameState.wait);

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));
        scGameLogic.FindAllMatches();

        if (otherGem != null)
        {
            if (isMatch == false && otherGem.isMatch == false)
            {
                otherGem.posIndex = posIndex;
                posIndex = previousPos;

                scGameLogic.SetGem(posIndex.x, posIndex.y, this);
                scGameLogic.SetGem(otherGem.posIndex.x, otherGem.posIndex.y, otherGem);

                await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));
                scGameLogic.SetState(GlobalEnums.GameState.move);
            }
            else
            {
                scGameLogic.DestroyMatches();
            }
        }
    }
}
