using BackupApp.Core.Models;
using BackupApp.Core.Storage;

namespace BackupApp.Core.Services;

public class VersionManager
{
    private readonly ManifestStore _manifestStore;
    private readonly BackupLayout _layout;

    public VersionManager(ManifestStore manifestStore, BackupLayout layout)
    {
        _manifestStore = manifestStore;
        _layout = layout;
    }

    public int Cleanup(BackupTask task)
    {
        if (task.RetentionTiers.Count == 0) return 0;

        var manifests = _manifestStore.ListManifests(task.DestPath, task.Name)
            .OrderByDescending(m => m.Timestamp)
            .ToList();

        if (manifests.Count == 0) return 0;

        var now = DateTime.Now;
        var toDelete = new HashSet<Guid>();

        for (int i = 0; i < task.RetentionTiers.Count; i++)
        {
            var tier = task.RetentionTiers[i];
            var lowerBound = i == 0 ? TimeSpan.Zero : TimeSpan.FromHours(task.RetentionTiers[i - 1].MaxAgeHours);
            var upperBound = tier.MaxAgeHours < 0 ? TimeSpan.MaxValue : TimeSpan.FromHours(tier.MaxAgeHours);

            var candidates = manifests
                .Where(m =>
                {
                    var age = now - m.Timestamp;
                    return age >= lowerBound && age < upperBound;
                })
                .OrderByDescending(m => m.Timestamp)
                .ToList();

            if (candidates.Count == 0) continue;

            var keep = new HashSet<Guid>();
            if (tier.KeepIntervalMinutes == 0)
            {
                if (tier.MaxAgeHours >= 0)
                {
                    keep.Add(candidates.First().RunId);
                }
                else
                {
                    keep.Clear();
                }
            }
            else
            {
                keep.Add(candidates[0].RunId);
                var interval = TimeSpan.FromMinutes(tier.KeepIntervalMinutes);
                var lastKept = candidates[0].Timestamp;

                for (int j = 1; j < candidates.Count; j++)
                {
                    if ((lastKept - candidates[j].Timestamp) >= interval)
                    {
                        keep.Add(candidates[j].RunId);
                        lastKept = candidates[j].Timestamp;
                    }
                }
            }

            foreach (var m in candidates)
            {
                if (!keep.Contains(m.RunId))
                    toDelete.Add(m.RunId);
            }
        }

        foreach (var runId in toDelete)
        {
            _manifestStore.DeleteManifest(task.DestPath, task.Name, runId);
        }

        return toDelete.Count;
    }
}
