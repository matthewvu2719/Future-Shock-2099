using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class MatchManager : MonoBehaviourPunCallbacks, IOnEventCallback
{

    public static MatchManager instance;
    private void Awake()
    {
        instance = this;
    }

    public enum EventCodes : byte
    {
        NewPlayer,
        ListPlayer,
        UpdateStat,
        NextMatch
    } 

    public List<PlayerInfo> allPlayers = new List<PlayerInfo>();
    private int index;

    private List<LeaderboardPlayer> lboardPlayers = new List<LeaderboardPlayer>();

    public enum GameState
    {
        Waiting,
        Playing,
        Ending
    }

    public int killsToWin = 3;
    public Transform mapCamPoint;
    public GameState state = GameState.Waiting;
    public float waitAfterEnding = 5f;

    public bool perpetual;

    // Start is called before the first frame update
    void Start()
    {
        if (!PhotonNetwork.IsConnected)
        {
            SceneManager.LoadScene(0);
        }
        else
        {
            NewPlayerSend(PhotonNetwork.NickName);
            state = GameState.Playing;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (state != GameState.Ending)
        {
            if (Input.GetKey(KeyCode.Tab))
            {
                ShowLeaderboard();
            }
            else
            {
                UIController.instance.leaderboard.SetActive(false);
            }
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        if(photonEvent.Code < 200)
        {
            EventCodes theEvent = (EventCodes)photonEvent.Code;
            object[] data = (object[])photonEvent.CustomData;

           // Debug.Log("Received event: " + theEvent);
            switch (theEvent)
            {
                case EventCodes.NewPlayer:
                    NewPlayerReceive(data);
                    break;

                case EventCodes.ListPlayer:
                    ListPlayersReceive(data);
                    break;

                case EventCodes.UpdateStat:
                    UpdateStatsReceive(data);
                    break;

                case EventCodes.NextMatch:
                    NextMatchReceive();
                    break;
            }
        }
    }

    public override void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void NewPlayerSend(string username)
    {
        object[] package = new object[4];
        package[0] = username;
        package[1] = PhotonNetwork.LocalPlayer.ActorNumber;
        package[2] = 0;
        package[3] = 0;


        PhotonNetwork.RaiseEvent(
            (byte)EventCodes.NewPlayer,
            package,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
            new SendOptions { Reliability = true }
            );

    }

    public void NewPlayerReceive(object[] data)
    {
        PlayerInfo player = new PlayerInfo((string)data[0],(int) data[1],(int) data[2],(int) data[3]);
        allPlayers.Add(player);
        ListPlayersSend();
    }

    public void ListPlayersSend()
    {
        object[] package = new object[allPlayers.Count +1];

        package[0] = state;

        for (int i =0; i< allPlayers.Count; i++)
        {
            object[] piece = new object[4];
            
            piece[0] = allPlayers[i].name;
            piece[1] = allPlayers[i].actor;
            piece[2] = allPlayers[i].kills;
            piece[3] = allPlayers[i].deaths;

            package[i +1] = piece;
        }

        PhotonNetwork.RaiseEvent(
            (byte)EventCodes.ListPlayer,
            package,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            new SendOptions { Reliability = true }
            );
    }

    public void ListPlayersReceive(object[] data)
    {
        allPlayers.Clear();
        state = (GameState)data[0];

        for (int i = 1; i< data.Length; i++)
        {
            object[] piece = (object[])data[i];
            PlayerInfo player = new PlayerInfo((string)piece[0], (int)piece[1], (int)piece[2], (int)piece[3]);
            allPlayers.Add(player);

            if(PhotonNetwork.LocalPlayer.ActorNumber == player.actor)
            {
                index = i-1;
            }
        }
        StateCheck();
    }

    public void UpdateStatsSend(int actorSending, int statToUpdate, int amountToChange)
    {
        object[] package = new object[] { actorSending, statToUpdate, amountToChange };
        
        PhotonNetwork.RaiseEvent(
            (byte)EventCodes.UpdateStat,
            package,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            new SendOptions { Reliability = true }
            );

    }

    public void UpdateStatsReceive(object[] data)
    {
        int actor = (int)data[0];
        int statType = (int)data[1];
        int amount = (int)data[2];

        for(int i=0; i< allPlayers.Count;i++)
        {
            if(allPlayers[i].actor == actor)
            {
                switch (statType)
                {
                    case 0: //kills
                        allPlayers[i].kills += amount;
                        Debug.Log("" + allPlayers[i].name + " kills " + allPlayers[i].kills);
                        break;
                    case 1: //deaths
                        allPlayers[i].deaths += amount;
                        Debug.Log("" + allPlayers[i].name + " deaths " + allPlayers[i].deaths);
                        break;
                }

                if (i == index)
                {
                    UpdateStatsDisplay();
                }

                if (UIController.instance.leaderboard.activeInHierarchy)
                {
                    ShowLeaderboard();
                }

                break;
            }
        }

        ScoreCheck();
    }

    public void UpdateStatsDisplay()
    {
        if (allPlayers.Count > index)
        {
            UIController.instance.killsText.text = "Kills: " + allPlayers[index].kills;
            UIController.instance.deathsText.text = "Deaths: " + allPlayers[index].deaths;
        }
        else
        {
            UIController.instance.killsText.text = "Kills: 0"; 
            UIController.instance.deathsText.text = "Deaths: 0";
        }
    }

    void ShowLeaderboard()
    {
        UIController.instance.leaderboard.SetActive(true);

        foreach(LeaderboardPlayer lp in lboardPlayers)
        {
            Destroy(lp.gameObject);
        }
        lboardPlayers.Clear();

        UIController.instance.leaderboardPlayerDisplay.gameObject.SetActive(false);

        List<PlayerInfo> sorted = SortPlayers(allPlayers);

        foreach(PlayerInfo player in sorted)
        {
            LeaderboardPlayer newPlayerDisplay = Instantiate(UIController.instance.leaderboardPlayerDisplay, UIController.instance.leaderboardPlayerDisplay.transform.parent);
            newPlayerDisplay.SetDetails(player.name, player.kills, player.deaths);
            newPlayerDisplay.gameObject.SetActive(true);
            lboardPlayers.Add(newPlayerDisplay);
        }
    }

    private List<PlayerInfo> SortPlayers(List<PlayerInfo> players)
    {
        List<PlayerInfo> sorted = new List<PlayerInfo>();
       
        while(sorted.Count< players.Count)
        {
            int highest = -1;
            PlayerInfo selectedPlayer = players[0];

            foreach(PlayerInfo player in players)
            {
                if (!sorted.Contains(player))
                {

                    if (player.kills > highest)
                    {
                        selectedPlayer = player;
                        highest = player.kills;
                    }
                }
            }

            sorted.Add(selectedPlayer);
        }
        return sorted;
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        SceneManager.LoadScene(0);
    }

    void ScoreCheck()
    {
        bool winnerFound = false;
        foreach(PlayerInfo player in allPlayers)
        {
            if(player.kills >= killsToWin && killsToWin > 0)
            {
                winnerFound = true;
                break;
            }
        }
        if (winnerFound)
        {
            if(PhotonNetwork.IsMasterClient && state != GameState.Ending)
            {
                state = GameState.Ending;
                ListPlayersSend();
            }
        }
    }

    void StateCheck()
    {
        if(state == GameState.Ending)
        {
            EndGame();
        }
    }

    void EndGame()
    {
        state = GameState.Ending;
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.DestroyAll();
        }

        UIController.instance.endScreen.SetActive(true);
        ShowLeaderboard();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Camera.main.transform.position = mapCamPoint.position;
        Camera.main.transform.rotation = mapCamPoint.rotation;
        StartCoroutine(EndCo());
    }

    private IEnumerator EndCo()
    {
        yield return new WaitForSeconds(waitAfterEnding);

        if (!perpetual)
        {
            PhotonNetwork.AutomaticallySyncScene = false;
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            if (PhotonNetwork.IsMasterClient)
            {
                NextMatchSend();
            }
        }
    
    
    }

    public void NextMatchSend()
    {
        PhotonNetwork.RaiseEvent(
            (byte)EventCodes.NextMatch,
            null,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            new SendOptions { Reliability = true }
    );

    }

    public void NextMatchReceive()
    {
        state = GameState.Playing;
        UIController.instance.endScreen.SetActive(false);
        UIController.instance.leaderboard.SetActive(false);
        foreach(PlayerInfo player in allPlayers)
        {
            player.kills = 0;
            player.deaths = 0;
        }

        UpdateStatsDisplay();
        PlayerSpawner.instance.SpawnPlayer();
    }
}

[System.Serializable]
public class PlayerInfo
{
    public string name;
    public int actor, kills, deaths;

    public PlayerInfo(string name, int actor, int kills, int deaths) 
    {
        this.name = name;
        this.actor = actor;
        this.kills = kills;
        this.deaths = deaths;
    }


}