using UnityEngine;

// The run's score (one team score in co-op):
//   knockouts: 100 / 150 / 200 for a level 1 / 2 / 3 worker, 2500 for a boss
//   combo: every knockout within 3 s of the last adds ×0.1 (up to ×2)
//   round clear: 250 × the round number, +500 if nobody got hurt that round
//   endless: everything is worth 10% more per round past round 10
// RoundManager feeds it; the HUD shows it; Finish() puts the run on the Leaderboard.
public static class ScoreKeeper
{
    public const float ComboWindow = 3f;
    public static int Score { get; private set; }
    public static int Kills { get; private set; }
    public static int Combo { get; private set; }                // knockouts in the current chain
    public static float ComboTime { get; private set; }          // seconds left to keep the chain going
    public static float Multiplier => 1f + Mathf.Min(Mathf.Max(Combo - 1, 0), 10) * 0.1f;
    public static bool Active { get; private set; }
    public static bool Endless { get; private set; }
    public static int LastRank { get; private set; } = -1;       // 1-based place on the board after Finish, -1 if not placed
    public static bool NewBest { get; private set; }
    public static int LastGain { get; private set; }
    public static float GainTime { get; private set; }

    static bool hurtThisRound, finished;
    static int designRounds = 10;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Active = false; Score = 0; Kills = 0; Combo = 0; LastRank = -1; NewBest = false; }

    public static void StartRun(bool endless, int storyRounds)
    {
        Active = true; Endless = endless; finished = false; designRounds = storyRounds;
        Score = 0; Kills = 0; Combo = 0; ComboTime = 0; LastRank = -1; NewBest = false; hurtThisRound = false; LastGain = 0; GainTime = 0;
    }

    static float RoundBonus(int round) => round > designRounds ? 1f + 0.1f * (round - designRounds) : 1f;

    static void Add(int points) { if (points <= 0) return; Score += points; LastGain = points; GainTime = 1.2f; }

    public static void Knockout(int level, int round)
    {
        if (!Active || finished) return;
        Kills++;
        Combo = ComboTime > 0 ? Combo + 1 : 1;
        ComboTime = ComboWindow;
        int basePts = level >= 3 ? 200 : level == 2 ? 150 : 100;
        Add(Mathf.RoundToInt(basePts * Multiplier * RoundBonus(round)));
    }

    public static void Boss(int round) { if (Active && !finished) { Kills++; Add(Mathf.RoundToInt(2500 * RoundBonus(round))); } }

    public static void PlayerHurt() { hurtThisRound = true; }

    public static void RoundCleared(int round)
    {
        if (!Active || finished) return;
        Add(Mathf.RoundToInt((250 * round + (hurtThisRound ? 0 : 500)) * RoundBonus(round)));
        hurtThisRound = false;
    }

    public static void Tick(float dt)
    {
        if (ComboTime > 0 && (ComboTime -= dt) <= 0) { ComboTime = 0; Combo = 0; }
        if (GainTime > 0) GainTime -= dt;
    }

    // End of the run (everyone down, or the story won): put it on the board.
    public static void Finish(int roundReached, string who)
    {
        if (!Active || finished) return;
        finished = true;
        var board = Leaderboard.Load(Endless);
        int best = board.Count > 0 ? board[0].score : 0;
        LastRank = Leaderboard.Submit(Endless, new Leaderboard.Entry
        {
            score = Score, round = roundReached, kills = Kills, who = who, date = System.DateTime.Now.ToString("MMM d, yyyy"),
        });
        NewBest = LastRank == 1 && Score > best;
    }
}
