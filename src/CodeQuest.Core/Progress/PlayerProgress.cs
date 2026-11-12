using System.Text.Json;
using System.Text.Json.Serialization;
using CodeQuest.Core.Grimoire;
using CodeQuest.Core.Levels;

namespace CodeQuest.Core.Progress;

public sealed class PlayerProgress
{
    public const int CurrentVersion = 1;
    private const int FailuresBeforeSolution = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, LevelRecord> _levels = [];
    private readonly HashSet<string> _viewedPages = [];

    public void RecordFailure(string levelId) => Record(levelId).Failures++;

    public void RecordHintUsed(string levelId) => Record(levelId).UsedHint = true;

    public void RecordSolutionSeen(string levelId) => Record(levelId).SawSolution = true;

    public void RecordPass(string levelId, int stars)
    {
        var record = Record(levelId);
        record.Completed = true;
        record.Stars = Math.Max(record.Stars, stars);
    }

    public void MarkPageViewed(string pageId) => _viewedPages.Add(pageId);

    public int StarsFor(string levelId) => Find(levelId)?.Stars ?? 0;

    public bool IsCompleted(string levelId) => Find(levelId)?.Completed ?? false;

    public bool UsedHint(string levelId) => Find(levelId)?.UsedHint ?? false;

    public bool SawSolution(string levelId) => Find(levelId)?.SawSolution ?? false;

    public bool CanSeeSolution(string levelId) => (Find(levelId)?.Failures ?? 0) >= FailuresBeforeSolution;

    public bool IsUnlocked(IReadOnlyList<Level> orderedLevels, string levelId)
    {
        var index = IndexOf(orderedLevels, levelId);
        return index == 0 || (index > 0 && IsCompleted(orderedLevels[index - 1].Id));
    }

    public bool IsPageAvailable(GrimoirePage page, IReadOnlyList<Level> orderedLevels) =>
        _viewedPages.Contains(page.Id)
        || orderedLevels.Any(level => level.GrimoirePageId == page.Id && IsUnlocked(orderedLevels, level.Id));

    public string ToJson()
    {
        var data = new ProgressData
        {
            Version = CurrentVersion,
            Levels = _levels,
            ViewedPages = [.. _viewedPages],
        };
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public static ProgressLoadResult FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ProgressLoadResult(new PlayerProgress(), false);
        }

        try
        {
            var data = JsonSerializer.Deserialize<ProgressData>(json, JsonOptions);
            if (data is null
                || data.Version != CurrentVersion
                || data.Levels is null
                || data.ViewedPages is null
                || data.Levels.Values.Any(record => record is null)
                || data.ViewedPages.Any(pageId => pageId is null))
            {
                return Unreadable();
            }

            var progress = new PlayerProgress();
            foreach (var (id, record) in data.Levels)
            {
                progress._levels[id] = record;
            }

            progress._viewedPages.UnionWith(data.ViewedPages);
            return new ProgressLoadResult(progress, false);
        }
        catch (JsonException)
        {
            return Unreadable();
        }
    }

    private static ProgressLoadResult Unreadable() => new(new PlayerProgress(), true);

    private static int IndexOf(IReadOnlyList<Level> levels, string levelId)
    {
        for (var i = 0; i < levels.Count; i++)
        {
            if (levels[i].Id == levelId)
            {
                return i;
            }
        }

        return -1;
    }

    private LevelRecord Record(string levelId)
    {
        if (!_levels.TryGetValue(levelId, out var record))
        {
            record = new LevelRecord();
            _levels[levelId] = record;
        }

        return record;
    }

    private LevelRecord? Find(string levelId) => _levels.GetValueOrDefault(levelId);

    private sealed class LevelRecord
    {
        public int Failures { get; set; }
        public bool UsedHint { get; set; }
        public bool SawSolution { get; set; }
        public bool Completed { get; set; }
        public int Stars { get; set; }
    }

    private sealed class ProgressData
    {
        public int Version { get; set; }
        public Dictionary<string, LevelRecord>? Levels { get; set; } = [];
        public List<string>? ViewedPages { get; set; } = [];
    }
}
