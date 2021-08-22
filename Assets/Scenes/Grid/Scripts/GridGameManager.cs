using citdev;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;

namespace citdev {
public delegate void NoParamDelegate();
public delegate void TilesDelegate(List<GameTile> t);
public delegate void IntDelegate(int i);

[RequireComponent(typeof(GridInputManager))]
public class GridGameManager : MonoBehaviour
{
    public NoParamDelegate OnRoundEnd;

    BoardController Board;
    GridInputManager _gim;

    [SerializeField] public int HitPoints;
    [SerializeField] public int Armor;
    [SerializeField] public int Kills = 0;

    GameController_DDOL _gc;
    LineRenderer _lr;

    int enemyHp = 4;
    bool RoundHasEnded = false;
    int enemyDmg = 2;

    int COUNT_ROWS = 6;
    int COUNT_COLS = 6;

    int round = 1;
    public int KillRequirement = 15;
    public int tilesCleared = 0;
    public int RoundMoves = 0;
    [SerializeField] GameObject dmgPrefab;
    [SerializeField] GameObject hpPrefab;
    [SerializeField] GameObject apPrefab;
    [SerializeField] GameObject gpPrefab;
    [SerializeField] GameObject floaterParent;
    [SerializeField] GameObject tilePrefab;
    [SerializeField] Transform SelectionCountDoodad;
    List<GameTile> tiles = new List<GameTile>();

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
        _gim = GetComponent<GridInputManager>();
        _lr = gameObject.GetComponent<LineRenderer>();
    }

    void Start()
    {
        _gc = FindObjectOfType<GameController_DDOL>();

        // _gc.round += 1;
        round = _gc.round;
        SetEnemyStatsByRound();
        SetupCharacterForRound();
        for (var rowid = 0; rowid < COUNT_ROWS; rowid++)
            {
                for (var colid = 0; colid < COUNT_COLS; colid++)
                {
                    GameObject g = GameObject.Instantiate(
                        tilePrefab,
                        new Vector2(rowid, colid),
                        Quaternion.identity
                    );
                    GameTile tile = g.GetComponent<GameTile>();
                    _gim.AttachTileToGrid(tile);
                    tile.AssignPosition(colid, rowid);
                    tile.MaxHitPoints = enemyHp;
                    tile.HitPoints = enemyHp;
                    tile.Damage = enemyDmg;
                    tiles.Add(tile);
                }
            }
        Board = new BoardController(tiles, _gim, enemyHp, enemyDmg);
        Board.OnPlayerCollectedTiles += PlayerCollectedTiles;
        Board.OnTileAddedToSelection += HandleTileAddedToSelection;
        Board.OnSelectionChange += HandleSelectionChange;
        Board.OnMonstersAttack += HandleMonstersAttack;
        Board.RunGrid();
    }

    void HandleMonstersAttack(List<GameTile> monsters) {
        if (RoundHasEnded) return;
        int damageReceived = monsters.Count * enemyDmg;

        if (damageReceived == 0) return;
         
        int random = Random.Range(1, 4);
        _gc.PlaySound("Monster_Hit_" + random);
        FloatDamage(damageReceived);
        AssessAttack(damageReceived);
        foreach(GameTile monster in monsters) {
            monster.MonsterMenace();
        }
    }

    void HandleSelectionChange(List<GameTile> selection) {
        _lr.positionCount = selection.Count;
        _lr.SetPositions(
            selection.Select((o) => o.transform.position).ToArray()
        );
        if (selection.Count == 0) {
            SelectionCountDoodad.gameObject.SetActive(false);
        } else {
            SelectionCountDoodad.gameObject.SetActive(true);
            SelectionCountDoodad.position = selection.ElementAt(selection.Count - 1).gameObject.transform.position;
            SelectionCountDoodad.GetComponent<DOODAD_SelectionCount>().SetText(selection.Where((o) => o.tileType != TileType.Monster).ToList().Count + "");               
        }
    }

    void HandleTileAddedToSelection(GameTile tile) {
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

    void SetupCharacterForRound()
    {
        HitPoints = 15;
    }

    void SetEnemyStatsByRound()
    {

        enemyHp = Mathf.Min((int) Mathf.Ceil(round / 3f) + 1, 4);
        enemyDmg = Mathf.Min((int) Mathf.Ceil(round / 4f), 3);
    }

    void AssessAttack(int damage)
    {
        if (Armor >= damage)
        {
            Armor -= damage;
            return;
        }

        int remainingDmg = damage - Armor;
        if (Armor > 0)
        {
            ApplyArmorChange(-Armor);
        }

        ApplyHpChange(-remainingDmg);
    }

    void ApplyHpChange(int changeAmount)
    {
        HitPoints = Mathf.Clamp(HitPoints + changeAmount, 0, 15);

        if (HitPoints == 0)
        {
            // (e) PLAYER DIED
            DoLose();
        }
    }

    void ApplyArmorChange(int changeAmount)
    {
        Armor = Mathf.Clamp(Armor + changeAmount, 0, 10);
    }

    public void PlayerCollectedTiles(List<GameTile> collected)
    {
        RoundMoves++;

        int healthGained = collected
            .Where((o) => o.tileType == TileType.Heart)
            .ToList().Count;

        int armorGained = collected
            .Where((o) => o.tileType == TileType.Shield)
            .ToList().Count;

        List<GameTile> coinsCollected = collected.Where((o) => o.tileType == TileType.Coin).ToList();

        int coinGained = collected
            .Where((o) => o.tileType == TileType.Coin)
            .ToList().Count * 10;

        if (coinsCollected.Count > 0)
        {
            _gc.PlaySound("Coin_Collect");
            FloatGold(coinGained);
        }

        int damageDealt = collected
            .Where((o) => o.tileType == TileType.Sword)
            .ToList().Count;

        List<GameTile> enemies = collected
            .Where((o) => o.tileType == TileType.Monster).ToList();

        List<GameTile> clearableTiles = collected
            .Where((o) => o.tileType != TileType.Monster).ToList();

        if (healthGained != 0)
        {
            ApplyHpChange(healthGained);
            _gc.PlaySound("Heart_Use");
            FloatHeal(healthGained);
        }
        if (armorGained != 0)
        {
            ApplyArmorChange(armorGained);
            if (enemies.Count == 0){
                _gc.PlaySound("Shield_Use");
            }
            FloatArmor(armorGained);
        }
        
        _gc.CoinBalanceChange(coinGained);

        if (enemies.Count > 0 && damageDealt > 0)
        {
            _gc.PlaySound("Sword_Hit");
        }

        foreach (GameTile monster in enemies)
        {
            // (e) : HITTING A MONSTER
            monster.HitPoints -= damageDealt;
            if (monster.HitPoints <= 0)
            {
                // (e) : MONSTER DIED
                clearableTiles.Add(monster);
                OnMonsterKill();
            } else
            {
                // (e) : MONSTER SURVIVED ATTACK
            }

            if (armorGained > 0) {
                // stun the monster
                monster.Stun();
                _gc.PlaySound("Bash");
            }
        }
    }

    void OnMonsterKill()
    {
        Kills += 1;
        _gc.OnMonsterKilled();
        if (Kills >= KillRequirement)
        {
            DoVictory();
        }
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
        // FindObjectOfType<BoardController>()?.ToggleTileFreeze(true);
        _gc.PreviousRoundMoves = RoundMoves;
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
        _gc.PreviousRoundMoves = RoundMoves;
        yield return new WaitForSeconds(3f);
        _gc.round += 1;
        _gc.ChangeScene("RoundScore");
    }
}
}