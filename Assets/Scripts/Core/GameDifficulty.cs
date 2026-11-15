namespace PokaGoryu
{
    public enum Challenge { Gentle, Standard, Harsh }

    public static class GameDifficulty
    {
        public static Challenge Mode = Challenge.Standard;
        public static float DrainScale { get { return Mode == Challenge.Gentle ? 0.8f : Mode == Challenge.Harsh ? 1.15f : 1f; } }
        public static float OrbScale { get { return Mode == Challenge.Gentle ? 1.15f : Mode == Challenge.Harsh ? 0.9f : 1f; } }
        public static float ResonanceScale { get { return Mode == Challenge.Gentle ? 1.2f : Mode == Challenge.Harsh ? 0.9f : 1f; } }
        public static float PressureScale { get { return Mode == Challenge.Gentle ? 0.8f : Mode == Challenge.Harsh ? 1.2f : 1f; } }
        public static string Name(Challenge mode)
        {
            return mode == Challenge.Gentle ? "Мягкая" : mode == Challenge.Harsh ? "Суровая" : "Обычная";
        }
    }
}
