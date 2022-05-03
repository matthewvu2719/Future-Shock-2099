using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LeaderboardPlayer : MonoBehaviour
{
    public TMP_Text playerNameText, killsText, deathsText;
    public void SetDetails(string name, int kills, int deaths)
    {
        playerNameText.text = name;
        killsText.text = kills.ToString();
        deathsText.text = deaths.ToString();
    }
}
