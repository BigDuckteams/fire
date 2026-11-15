using System;
using System.Diagnostics;
using System.IO;
using PokaGoryu;

// Standalone: csc /out:MusicChecks.exe MusicChecks.cs ../../Assets/Scripts/Core/SuspenseScore.cs
public static class MusicChecks
{
    public static int Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : ".";
        Directory.CreateDirectory(output);
        Stopwatch timer = Stopwatch.StartNew();
        float[][] stems = new float[SuspenseScore.Layers][];
        for (int s = 0; s < stems.Length; s++)
        {
            stems[s] = SuspenseScore.Render(s);
            double peak = 0, sum = 0, jump = 0;
            for (int i = 0; i < stems[s].Length; i++)
            {
                float v = stems[s][i];
                if (float.IsNaN(v) || float.IsInfinity(v)) throw new Exception("Non-finite sample");
                peak = Math.Max(peak, Math.Abs(v));
                sum += v * v;
                if (i >= 2) jump = Math.Max(jump, Math.Abs(v - stems[s][i - 2]));
            }
            double seam = Math.Max(Math.Abs(stems[s][0] - stems[s][stems[s].Length - 2]),
                                   Math.Abs(stems[s][1] - stems[s][stems[s].Length - 1]));
            Console.WriteLine("stem {0}: peak={1:F5}, RMS={2:F5}, seam={3:F6}, largest step={4:F6}",
                s, peak, Math.Sqrt(sum / stems[s].Length), seam, jump);
            if (peak > 0.5 || peak < 0.01 || seam > Math.Max(0.001, jump * 1.1))
                throw new Exception("Headroom / silent stem / loop seam check failed");
        }
        float[] again = SuspenseScore.Render(1);
        for (int i = 0; i < again.Length; i++)
            if (again[i] != stems[1][i]) throw new Exception("Score is not deterministic");
        double maxSum = 0;
        for (int i = 0; i < stems[0].Length; i++)
        {
            // Absolute sum is stricter than the actual maximum mix (65/80/90/85 percent).
            double sum = 0;
            for (int s = 0; s < stems.Length; s++) sum += Math.Abs(stems[s][i]);
            maxSum = Math.Max(maxSum, sum);
        }
        if (maxSum >= 0.8) throw new Exception("Insufficient music headroom");
        Console.WriteLine("worst-case sum peak={0:F5}; synthesis + determinism check {1:F2}s", maxSum, timer.Elapsed.TotalSeconds);

        // Two phrases: exploration -> danger -> final stand -> a quiet hearth.
        int frames = SuspenseScore.Frames * 2;
        float[] preview = new float[frames * 2];
        double[] gain = new double[4];
        double intensity = 0.1;
        for (int f = 0; f < frames; f++)
        {
            double t = f / (double)SuspenseScore.Rate;
            double target = t < 8 ? 0.1 : t < 20 ? 0.1 + (t - 8) * 0.9 / 12 : t < 34 ? 1 : 0.03;
            intensity += (target - intensity) * (1 - Math.Exp(-(target > intensity ? 0.9 : 0.2) / SuspenseScore.Rate));
            double[] desired = { 0.65, 0.48 + 0.32 * intensity,
                0.90 * Smooth((intensity - 0.12) / 0.73), 0.85 * Smooth((intensity - 0.42) / 0.58) };
            for (int s = 0; s < 4; s++)
                gain[s] += Math.Sign(desired[s] - gain[s]) * Math.Min(Math.Abs(desired[s] - gain[s]), 0.45 / SuspenseScore.Rate);
            double fade = Math.Min(1, Math.Min(t / 0.1, (frames / (double)SuspenseScore.Rate - t) / 1.5));
            for (int ch = 0; ch < 2; ch++)
            {
                double v = 0;
                for (int s = 0; s < 4; s++) v += stems[s][(f % SuspenseScore.Frames) * 2 + ch] * gain[s];
                preview[f * 2 + ch] = (float)(v * 0.65 * 0.8 * 0.8 * fade);
            }
        }
        WriteWav(Path.Combine(output, "suspense-preview.wav"), preview);
        Console.WriteLine("MUSIC CHECKS: OK; 48-second preview uses default music / master gain");
        return 0;
    }

    static double Smooth(double t) { t = Math.Max(0, Math.Min(1, t)); return t * t * (3 - 2 * t); }

    static void WriteWav(string file, float[] data)
    {
        using (BinaryWriter w = new BinaryWriter(File.Create(file)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + data.Length * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)2); w.Write(SuspenseScore.Rate);
            w.Write(SuspenseScore.Rate * 4); w.Write((short)4); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(data.Length * 2);
            for (int i = 0; i < data.Length; i++)
                w.Write((short)Math.Round(Math.Max(-1, Math.Min(1, data[i])) * 32767));
        }
    }
}
