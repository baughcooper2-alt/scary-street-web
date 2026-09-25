// Everyone from DESIGN.md's Characters section, in select-screen order.
// A character is playable when GameFlow has a CharacterLook whose displayName matches.
public static class CharacterRoster
{
    public enum StartingWeapon { None, LawBook, Guitar }

    public class Entry
    {
        public readonly string name, startsWith;
        public readonly bool dlc;
        public readonly StartingWeapon weapon;
        public Entry(string name, string startsWith, bool dlc = false, StartingWeapon weapon = StartingWeapon.None)
        { this.name = name; this.startsWith = startsWith; this.dlc = dlc; this.weapon = weapon; }
    }

    public static Entry Find(string name) => System.Array.Find(All, e => e.name == name);

    public static readonly Entry[] All =
    {
        new Entry("Cooper", "Law Book", weapon: StartingWeapon.LawBook),
        new Entry("Nathan", "Guitar", weapon: StartingWeapon.Guitar),
        new Entry("Isaiah", "Skateboard (no weapon)"),
        new Entry("John", "6-pack of beer"),
        new Entry("Will", "No weapon; alcohol never makes him dizzy"),
        new Entry("Piper", "Crutch"),
        new Entry("Thorton", "No weapon; starts with $100"),
        new Entry("Kenny", "Box of Goldfish"),

        new Entry("Sassy", "DLC", true),
        new Entry("Donnie", "DLC", true),
        new Entry("DJ Shaq", "DLC", true),
        new Entry("Talan", "DLC", true),
        new Entry("Arry", "DLC", true),
        new Entry("Lily", "DLC", true),
        new Entry("Mordecai", "DLC", true),
        new Entry("Rigby", "DLC", true),
    };
}
