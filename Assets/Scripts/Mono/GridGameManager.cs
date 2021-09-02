using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;

public delegate void ActorTilesDelegate(List<GameTile> t);

public class GridGameManager : MonoBehaviour
{
    public NoParamDelegate OnRoundEnd;

    GameController_DDOL _gc;
    LineRenderer _lr;
    GridInputManager _gim;

    bool RoundHasEnded = false;
    public GameBoard Board;

    [SerializeField] GameObject dmgPrefab;
    [SerializeField] GameObject hpPrefab;
    [SerializeField] GameObject apPrefab;
    [SerializeField] GameObject gpPrefab;
    [SerializeField] GameObject floaterParent;
    [SerializeField] Transform SelectionCountDoodad;

    void FloatDamage(int dmg)
    {
        var go = Instantiate(dmgPrefab, floaterParent.transform);
        go.GetComponent<TextMeshProUGUI>().text = "-" + dmg + " HP";
    }
    void FloatHeal(int dmg)
    {
        var go = Instantiate(hpPrefab, floaterParent.transform);
        go.GetComponent<TextMeshProUGUI>().text = "+" + dmg + " HP";
    }
    void FloatArmor(int dmg)
    {
        var go = Instantiate(apPrefab, floaterParent.transform);
        go.GetComponent<TextMeshProUGUI>().text = "+" + dmg + " AP";
    }
    void FloatGold(int dmg)
    {
        var go = Instantiate(gpPrefab, floaterParent.transform);
        go.GetComponent<TextMeshProUGUI>().text = "+" + dmg + " GP";
    }

    void Awake() {
        _lr = gameObject.GetComponent<LineRenderer>();
    }

    void Start()
    {
        _gc = FindObjectOfType<GameController_DDOL>();
        _gim = gameObject.GetComponent<GridInputManager>();

        BoardContext bctx = new BoardContext(
            _gc.CurrentCharacter,
            _gc.round
        );
        Board = new GameBoard(bctx);

        foreach (Tile tile in Board.Tiles) {
            GameObject tilePrefab = Resources.Load<GameObject>("Prefabs/Tile");
            GameObject go = Instantiate(
                tilePrefab,
                tile.GridPosition(),
                Quaternion.identity
            );
            GameTile gt = go.GetComponent<GameTile>();
            gt.AttachToTile(tile);
            _gim.AttachTileToGrid(gt);
        }
        Board.OnPlayerCollectedTiles += HandlePlayerCollectedTiles;
        Board.OnTileAddedToSelection += HandleTileAddedToSelection;
        Board.OnSelectionChange += HandleSelectionChange;
        Board.OnMonstersAttack += HandleMonstersAttack;
        Board.OnCoinCollected += HandleCoinCollected;
        Board.OnHeartsCollected += HandleHeartsCollected;
        Board.OnShieldsCollected += HandleShieldsCollected;
        Board.OnSwordsCollected += HandleSwordsCollected;
        Board.OnEnemyStunned += HandleMonsterStunned;
        Board.OnMonsterKillEarned += HandleMonsterKillEarned;
        Board.OnWin += HandleWin;
        Board.OnLose += HandleLose;
        _gim.OnUserDragIndicatingTile += Board.UserIndicatingTile;
        _gim.OnUserStartSelection += Board.UserStartSelection;
        _gim.OnUserEndSelection += Board.UserEndSelection;
    }

    void OnDestroy() {
        if (Board == null) return;
        
        Board.OnPlayerCollectedTiles -= HandlePlayerCollectedTiles;
        Board.OnTileAddedToSelection -= HandleTileAddedToSelection;
        Board.OnSelectionChange -= HandleSelectionChange;
        Board.OnMonstersAttack -= HandleMonstersAttack;
        Board.OnCoinCollected -= HandleCoinCollected;
        Board.OnHeartsCollected -= HandleHeartsCollected;
        Board.OnShieldsCollected -= HandleShieldsCollected;
        Board.OnSwordsCollected -= HandleSwordsCollected;
        Board.OnEnemyStunned -= HandleMonsterStunned;
        Board.OnMonsterKillEarned -= HandleMonsterKillEarned;
        Board.OnWin -= HandleWin;
        Board.OnLose -= HandleLose;
    }

    void HandlePlayerCollectedTiles(List<Tile> tiles) {

    }

    void HandleMonstersAttack(int damageReceived) {
        if (RoundHasEnded) return;
        int random = Random.Range(1, 4);
        _gc.PlaySound("Monster_Hit_" + random);
        FloatDamage(damageReceived);
    }

    void HandleSelectionChange(List<Tile> selection) {
        _lr.positionCount = selection.Count;
        _lr.SetPositions(
            selection.Select((o) => o.GridPosition()).ToArray()
        );
        if (selection.Count == 0) {
            SelectionCountDoodad.gameObject.SetActive(false);
        } else {
            SelectionCountDoodad.gameObject.SetActive(true);
            SelectionCountDoodad.position = selection.ElementAt(selection.Count - 1).GridPosition();
            SelectionCountDoodad.GetComponent<DOODAD_SelectionCount>().SetText(selection.Where((o) => o.tileType != TileType.Monster).ToList().Count + "");               
        }
    }

    void HandleTileAddedToSelection(Tile tile) {
        switch (tile.tileType) {
            case TileType.Coin:
                _gc.PlaySound("Coin_Select");
                break;
            case TileType.Heart:
                _gc.PlaySound("Heart_Select");
                break;
            case TileType.Shield:
                _gc.PlaySound("Shield_Select");
                break;

            default:
                _gc.PlaySound("Sword_Select");
                break;
        }
    }

    void AssessAttack(int damage)
    {
        
    }

    void HandleWin() {
        DoVictory();
    }

    void HandleLose() {
        DoLose();
    }
    void HandleMonsterKillEarned() {
        _gc.OnMonsterKilled();
    }
    void HandleMonsterStunned() {
        _gc.PlaySound("Sword_Hit");
    }
    void HandleCoinCollected(int amt) {
        _gc.PlaySound("Coin_Collect");
        FloatGold(amt);
        _gc.CoinBalanceChange(amt);
    }

    void HandleHeartsCollected(int amt) {
        _gc.PlaySound("Heart_Use");
        FloatHeal(amt);
    }

    void HandleShieldsCollected(int amt) {
        _gc.PlaySound("Shield_Use");
        FloatArmor(amt);
    }

    void HandleSwordsCollected(int amt) {
        _gc.PlaySound("Sword_Hit");
    }

    void DoLose()
    {
        if (RoundHasEnded) return;
        RoundHasEnded = true;
        OnRoundEnd?.Invoke();
        StartCoroutine("LoseRoutine");
    }

    IEnumerator LoseRoutine()
    {
        _gc.PreviousRoundMoves = Board.MovesMade;
        yield return new WaitForSeconds(0.2f);
        _gc.ChangeScene("GameOver");
    }

    void DoVictory()
    {
        if (RoundHasEnded) return;
        RoundHasEnded = true;
        OnRoundEnd?.Invoke();
        StartCoroutine("RoundVictory");
    }

    IEnumerator RoundVictory()
    {
        _gc.PreviousRoundMoves = Board.MovesMade;
        yield return new WaitForSeconds(3f);
        _gc.round += 1;
        _gc.ChangeScene("RoundScore");
    }
}