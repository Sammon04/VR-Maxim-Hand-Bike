using UnityEngine;

public enum GameMode
{
    TimeTrial,
    NPCRace,
    Casual,
    None
}

public static class ModeSettings
{
    public static GameMode Mode;
}
