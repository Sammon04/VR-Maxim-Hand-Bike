using System;
using UnityEngine;

public class ModeInitializer : MonoBehaviour
{
    [SerializeField] GameObject timeTrialLogic;
    //[SerializeField] GameObject npcRaceLogic;
    //[SerializeField] GameObject casualLogic;

    void Start()
    {
        switch (ModeSettings.Mode)
        {
            case GameMode.TimeTrial:
                timeTrialLogic.SetActive(true);
                //npcRaceLogic.SetActive(false);
                //casualLogic.SetActive(false);
                Debug.Log("Time Trial Active");
                break;

            case GameMode.NPCRace:
                timeTrialLogic.SetActive(false);
                //npcRaceLogic.SetActive(true);
                //casualLogic.SetActive(false);
                Debug.Log("NPC Race Active");
                break;

            case GameMode.Casual:
                timeTrialLogic.SetActive(false);
                //npcRaceLogic.SetActive(false);
                //casualLogic.SetActive(true);
                Debug.Log("Casual Active");
                break;

            default:
                timeTrialLogic.SetActive(false);
                //npcRaceLogic.SetActive(false);
                //casualLogic.SetActive(false);
                Debug.Log("wtf");
                break;
        }
    }
}
