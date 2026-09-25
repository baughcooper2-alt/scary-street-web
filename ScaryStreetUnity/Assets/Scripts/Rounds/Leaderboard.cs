using System.Collections.Generic;
using UnityEngine;

// Best runs on this computer: top 10 for the story and for endless, kept in PlayerPrefs as JSON.
public static class Leaderboard
{
    public const int Size = 10;

    [System.Serializable]
    public class Entry { public int score, round, kills; public string who, date; }

    [System.Serializable] class Board { public List<Entry> entries = new List<Entry>(); }

    static string Key(bool endless) => endless ? "leaderboard_endless" : "leaderboard_story";

    public static List<Entry> Load(bool endless)
    {
        var json = PlayerPrefs.GetString(Key(endless), "");
        if (string.IsNullOrEmpty(json)) return new List<Entry>();
        try { return JsonUtility.FromJson<Board>(json)?.entries ?? new List<Entry>(); }
        catch { return new List<Entry>(); }
    }

    // Returns the run's 1-based place, or -1 if it didn't make the top 10.
    public static int Submit(bool endless, Entry e)
    {
        var list = Load(endless);
        list.Add(e);
        list.Sort((a, b) => b.score.CompareTo(a.score));
        int rank = list.IndexOf(e) + 1;
        if (list.Count > Size) list.RemoveRange(Size, list.Count - Size);
        PlayerPrefs.SetString(Key(endless), JsonUtility.ToJson(new Board { entries = list }));
        PlayerPrefs.Save();
        return rank <= Size ? rank : -1;
    }
}
