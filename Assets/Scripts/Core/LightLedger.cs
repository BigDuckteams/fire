using System;
using System.Text;

namespace PokaGoryu
{
    public enum LightReason
    {
        Other, Lamp, Orb, Resonance, Pizza, Hearth, Rescue, Respawn,
        Smolder, Aura, Shot, Beam, Flash, Guide, Wake, Heal,
        FeedLamp, FeedBus, FeedGenerator, Enemy, Decoy, Debug, Count
    }

    /// <summary>Actual, clamped light transfers. Fixed-size rolling minute; no allocations when recording.</summary>
    public sealed class LightLedger
    {
        const int Reasons = (int)LightReason.Count;
        readonly double[] income = new double[Reasons];
        readonly double[] expense = new double[Reasons];
        readonly double[,] minuteIncome = new double[60, Reasons];
        readonly double[,] minuteExpense = new double[60, Reasons];
        long second = -1;
        public double OpeningLight { get; private set; }
        public double TotalIncome { get; private set; }
        public double TotalExpense { get; private set; }
        public double ExpectedLight { get { return OpeningLight + TotalIncome - TotalExpense; } }

        public void Reset(double startingLight)
        {
            Array.Clear(income, 0, income.Length);
            Array.Clear(expense, 0, expense.Length);
            Array.Clear(minuteIncome, 0, minuteIncome.Length);
            Array.Clear(minuteExpense, 0, minuteExpense.Length);
            OpeningLight = startingLight;
            TotalIncome = TotalExpense = 0;
            second = -1;
        }

        public void Tick(double playTime)
        {
            long now = (long)Math.Floor(Math.Max(0, playTime));
            if (now <= second) return;
            long first = Math.Max(second + 1, now - 59);
            for (long s = first; s <= now; s++)
                for (int r = 0; r < Reasons; r++)
                {
                    minuteIncome[s % 60, r] = 0;
                    minuteExpense[s % 60, r] = 0;
                }
            second = now;
        }

        public void Record(LightReason reason, double delta, double playTime)
        {
            if (double.IsNaN(delta) || double.IsInfinity(delta)) throw new ArgumentOutOfRangeException("delta");
            if ((int)reason < 0 || reason >= LightReason.Count) throw new ArgumentOutOfRangeException("reason");
            Tick(playTime);
            int r = (int)reason, slot = (int)(second % 60);
            if (delta > 0) { income[r] += delta; minuteIncome[slot, r] += delta; TotalIncome += delta; }
            else if (delta < 0) { expense[r] -= delta; minuteExpense[slot, r] -= delta; TotalExpense -= delta; }
        }

        public double Amount(LightReason reason, bool incoming, bool recent)
        {
            if (!recent) return incoming ? income[(int)reason] : expense[(int)reason];
            double sum = 0;
            for (int s = 0; s < 60; s++) sum += incoming ? minuteIncome[s, (int)reason] : minuteExpense[s, (int)reason];
            return sum;
        }

        public string Breakdown(bool incoming, bool recent)
        {
            StringBuilder b = new StringBuilder();
            double total = 0;
            for (int r = 0; r < Reasons; r++)
            {
                double value = Amount((LightReason)r, incoming, recent);
                if (value < 0.005) continue;
                total += value;
                b.Append(Name((LightReason)r)).Append(": ").Append(value.ToString("0.0")).AppendLine();
            }
            if (b.Length == 0) b.AppendLine("Пока нет");
            return b.Append("Всего: ").Append(total.ToString("0.0")).ToString();
        }

        public static string Name(LightReason r)
        {
            switch (r)
            {
                case LightReason.Lamp: return "Лампы";
                case LightReason.Orb: return "Осколки";
                case LightReason.Resonance: return "Тепло людей";
                case LightReason.Pizza: return "Пицца";
                case LightReason.Hearth: return "Очаги";
                case LightReason.Rescue: return "Спасение людьми";
                case LightReason.Respawn: return "Возвращение к очагу";
                case LightReason.Smolder: return "Тление";
                case LightReason.Aura: return "Аура";
                case LightReason.Shot: return "Выстрелы";
                case LightReason.Beam: return "Слитый луч";
                case LightReason.Flash: return "Вспышки";
                case LightReason.Guide: return "Проводник";
                case LightReason.Wake: return "Пробуждение людей";
                case LightReason.Heal: return "Лечение людей";
                case LightReason.FeedLamp: return "Свет лампам";
                case LightReason.FeedBus: return "Свет автобусу";
                case LightReason.FeedGenerator: return "Свет генератору";
                case LightReason.Enemy: return "Урон врагов";
                case LightReason.Decoy: return "Ложные огни";
                case LightReason.Debug: return "Отладка";
                default: return "Прочее";
            }
        }

        public string Csv(double playTime, double currentLight, int deaths)
        {
            Tick(playTime);
            System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
            StringBuilder b = new StringBuilder("metric,value\n");
            b.Append("seconds,").Append(playTime.ToString("0.00", c)).AppendLine();
            b.Append("opening_light,").Append(OpeningLight.ToString("0.000", c)).AppendLine();
            b.Append("current_light,").Append(currentLight.ToString("0.000", c)).AppendLine();
            b.Append("expected_light,").Append(ExpectedLight.ToString("0.000", c)).AppendLine();
            b.Append("deaths,").Append(deaths).AppendLine();
            b.AppendLine("reason,received,spent,received_last_minute,spent_last_minute");
            for (int i = 0; i < Reasons; i++)
            {
                LightReason reason = (LightReason)i;
                if (income[i] == 0 && expense[i] == 0) continue;
                b.Append(reason).Append(',').Append(income[i].ToString("0.000", c)).Append(',').Append(expense[i].ToString("0.000", c));
                b.Append(',').Append(Amount(reason, true, true).ToString("0.000", c));
                b.Append(',').Append(Amount(reason, false, true).ToString("0.000", c)).AppendLine();
            }
            return b.ToString();
        }
    }
}
