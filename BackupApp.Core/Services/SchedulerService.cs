using BackupApp.Core.Models;
using BackupApp.Core.Storage;

namespace BackupApp.Core.Services;

public class SchedulerService : IDisposable
{
    private readonly ConfigStore _configStore;
    private readonly BackupEngine _engine;
    private Timer? _timer;
    private readonly HashSet<Guid> _runningTasks = new();

    public SchedulerService(ConfigStore configStore, BackupEngine engine)
    {
        _configStore = configStore;
        _engine = engine;
    }

    public void Start()
    {
        _timer = new Timer(CheckAndRun, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void CheckAndRun(object? state)
    {
        var tasks = _configStore.ListTasks().Where(t => t.Enabled && t.Schedule.Enabled).ToList();
        var now = DateTime.Now;

        foreach (var task in tasks)
        {
            if (_runningTasks.Contains(task.Id)) continue;

            DateTime? next = GetNextRun(task.Schedule.CronExpression, task.Schedule.NextRun, now);
            if (next.HasValue && next.Value <= now)
            {
                _runningTasks.Add(task.Id);
                ThreadPool.QueueUserWorkItem(async _ =>
                {
                    try
                    {
                        await _engine.RunBackupAsync(task.Id);
                    }
                    catch { /* logged in engine */ }
                    finally
                    {
                        _runningTasks.Remove(task.Id);
                    }
                });

                task.Schedule.NextRun = GetNextRun(task.Schedule.CronExpression, null, now);
                _configStore.SaveTask(task);
            }
        }
    }

    public static DateTime? GetNextRun(string cronExpression, DateTime? lastRun, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(cronExpression)) return null;

        try
        {
            var parts = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) return null;

            var minute = ParseField(parts[0], 0, 59);
            var hour = ParseField(parts[1], 0, 23);
            var dayOfMonth = ParseField(parts[2], 1, 31);
            var month = ParseField(parts[3], 1, 12);
            var dayOfWeek = ParseField(parts[4], 0, 6);

            var candidate = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddMinutes(1);

            for (int i = 0; i < 525600; i++) // search up to 1 year ahead
            {
                candidate = candidate.AddMinutes(1);
                if (candidate.Year > now.Year + 1) break;

                if (!MatchField(minute, candidate.Minute)) continue;
                if (!MatchField(hour, candidate.Hour)) continue;
                if (!MatchField(dayOfMonth, candidate.Day)) continue;
                if (!MatchField(month, candidate.Month)) continue;
                if (!MatchField(dayOfWeek, (int)candidate.DayOfWeek)) continue;

                return candidate;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static HashSet<int> ParseField(string field, int min, int max)
    {
        var result = new HashSet<int>();
        if (field == "*")
        {
            for (int i = min; i <= max; i++) result.Add(i);
            return result;
        }

        foreach (var part in field.Split(','))
        {
            if (part.Contains('/'))
            {
                var split = part.Split('/');
                var range = split[0];
                var step = int.Parse(split[1]);
                if (range == "*")
                {
                    for (int i = min; i <= max; i += step) result.Add(i);
                }
                else
                {
                    var rangeParts = range.Split('-');
                    var start = int.Parse(rangeParts[0]);
                    var end = rangeParts.Length > 1 ? int.Parse(rangeParts[1]) : max;
                    for (int i = start; i <= end; i += step) result.Add(i);
                }
            }
            else if (part.Contains('-'))
            {
                var rangeParts = part.Split('-');
                var start = int.Parse(rangeParts[0]);
                var end = int.Parse(rangeParts[1]);
                for (int i = start; i <= end; i++) result.Add(i);
            }
            else
            {
                result.Add(int.Parse(part));
            }
        }
        return result;
    }

    private static bool MatchField(HashSet<int> field, int value)
    {
        return field.Contains(value);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
