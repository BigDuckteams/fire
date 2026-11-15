using System;

namespace PokaGoryu
{
    /// <summary>Original eight-bar score, E Phrygian, 80 BPM. No files or external samples.</summary>
    public static class SuspenseScore
    {
        public const int Rate = 32000;
        public const int Layers = 4;
        public const double Beat = 0.75;
        public const double Duration = 32 * Beat;
        public const int Frames = Rate * 24;
        const double Tau = Math.PI * 2.0;

        // Layer order: dark pad, distant motif, bass pulse, bowed tension / metal.
        public static float[] Render(int layer)
        {
            if (layer < 0 || layer >= Layers) throw new ArgumentOutOfRangeException("layer");
            float[] data = new float[Frames * 2];
            if (layer == 0) Pad(data);
            else if (layer == 1) Motif(data);
            else if (layer == 2) Pulse(data);
            else Pressure(data);
            // Fixed gain preserves the balance between layers and leaves headroom for effects.
            for (int i = 0; i < data.Length; i++) data[i] *= 1.32f;
            return data;
        }

        static double Hz(int midi) { return 440.0 * Math.Pow(2.0, (midi - 69) / 12.0); }
        static double LoopHz(double hz) { return Math.Round(hz * Duration) / Duration; }
        static double Sin(double phase) { return Math.Sin(Tau * phase); }

        static void Pad(float[] data)
        {
            double low = LoopHz(Hz(28)), root = LoopHz(Hz(40));
            double fifth = LoopHz(Hz(47)), minor = LoopHz(Hz(55));
            double rub = LoopHz(Hz(53));
            for (int i = 0; i < Frames; i++)
            {
                double t = i / (double)Rate;
                double breath = 0.70 + 0.18 * Sin(t / 12.0) + 0.08 * Sin(t / 8.0);
                double sub = 0.075 * Sin(low * t) + 0.045 * Sin(root * t);
                double l = 0.060 * Sin(fifth * t + 0.13 * Sin(t / 6.0))
                         + 0.038 * Sin(minor * t) + 0.017 * Sin(rub * t);
                double r = 0.060 * Sin((fifth + 1.0 / Duration) * t + 0.13 * Sin(t / 6.0))
                         + 0.038 * Sin((minor - 1.0 / Duration) * t) + 0.017 * Sin(rub * t + 0.2);
                data[i * 2] = (float)(sub + l * breath);
                data[i * 2 + 1] = (float)(sub + r * breath);
            }
        }

        static void Motif(float[] data)
        {
            // The semitone above the tonic never resolves until the next phrase.
            int[] notes = { 64, 71, 65, 64, 62, 59, 65, 64, 67, 65, 59, 64 };
            double[] at = { 0.5, 3.5, 6, 8.5, 11.5, 14, 16.5, 19.5, 22, 24.5, 27, 30 };
            for (int i = 0; i < notes.Length; i++)
            {
                double pan = i % 2 == 0 ? -0.38 : 0.38;
                double level = i % 3 == 0 ? 0.20 : 0.14;
                Tone(data, at[i] * Beat, Hz(notes[i]), 3.8, level, pan, 0);
                Tone(data, at[i] * Beat + 0.375, Hz(notes[i]), 3.8, level * 0.30, -pan, 0);
                Tone(data, at[i] * Beat + 0.825, Hz(notes[i]), 4.3, level * 0.14, pan * 0.5, 0);
            }
        }

        static void Pulse(float[] data)
        {
            int[] bass = { 40, 40, 47, 41, 40, 43, 47, 41 };
            for (int i = 0; i < 32; i++)
            {
                double at = i * Beat;
                Tone(data, at, Hz(bass[(i / 4) % bass.Length]), 0.65, i % 4 == 0 ? 0.27 : 0.19, 0.0, 1);
                Tone(data, at + Beat * 0.5, Hz(52 + (i % 4 == 3 ? 1 : 0)), 0.24, 0.065, i % 2 == 0 ? -0.28 : 0.28, 1);
            }
        }

        static void Pressure(float[] data)
        {
            for (int i = 0; i < Frames; i++)
            {
                double t = i / (double)Rate;
                double swell = 0.25 + 0.75 * Math.Pow(0.5 - 0.5 * Math.Cos(Tau * t / 12.0), 2);
                double trem = 0.65 + 0.35 * Sin(t * 8.0);
                double l = Sin(LoopHz(Hz(64)) * t + 0.12 * Sin(t / 3.0))
                         + 0.55 * Sin(LoopHz(Hz(65)) * t) + 0.30 * Sin(LoopHz(Hz(70)) * t);
                double r = Sin(LoopHz(Hz(64) + 0.4) * t + 0.12 * Sin(t / 3.0))
                         + 0.55 * Sin(LoopHz(Hz(65) - 0.3) * t) + 0.30 * Sin(LoopHz(Hz(70)) * t + 0.3);
                data[i * 2] = (float)(l * 0.052 * swell * trem);
                data[i * 2 + 1] = (float)(r * 0.052 * swell * trem);
            }
            // Sparse metallic echoes, without a loud jumpscare or a constant hiss.
            Tone(data, 14 * Beat, Hz(76), 5.5, 0.12, -0.65, 2);
            Tone(data, 30 * Beat, Hz(77), 5.5, 0.12, 0.65, 2);
        }

        static void Tone(float[] data, double start, double hz, double length, double level, double pan, int voice)
        {
            int first = (int)Math.Round(start * Rate);
            int count = (int)(length * Rate);
            double left = Math.Sqrt((1.0 - pan) * 0.5), right = Math.Sqrt((1.0 + pan) * 0.5);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate;
                double attack = 1.0 - Math.Exp(-t * (voice == 2 ? 7 : 180));
                double tail = Math.Min(1.0, (length - t) / 0.12);
                double v;
                if (voice == 0)
                    v = (Sin(hz * t) + 0.25 * Sin(hz * 2.002 * t) * Math.Exp(-t * 2.4)
                       + 0.12 * Sin(hz * 3.99 * t) * Math.Exp(-t * 5.0)) * Math.Exp(-t * 1.5);
                else if (voice == 1)
                    v = (Sin(hz * t) + 0.25 * Sin(hz * 2 * t) + 0.10 * Sin(hz * 3 * t)) * Math.Exp(-t * 8.0);
                else
                    v = (Sin(hz * t) + 0.38 * Sin(hz * 1.483 * t) + 0.20 * Sin(hz * 2.713 * t)) * Math.Exp(-t * 0.9);
                v *= level * attack * tail;
                // Wrap release and echo tails into the beginning: the loop stays continuous.
                int frame = (first + i) % Frames;
                data[frame * 2] += (float)(v * left);
                data[frame * 2 + 1] += (float)(v * right);
            }
        }
    }
}
