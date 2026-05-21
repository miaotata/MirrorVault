namespace BackupApp.Core.Models;

public enum BackupMode
{
    OneWay,
    Incremental,
    TwoWaySync
}

public enum DestType
{
    Local,
    Network,
    Cloud
}

public enum BackupStatus
{
    Success,
    Failed,
    Partial
}

public enum FileStatus
{
    Added,
    Modified,
    Deleted,
    Unchanged
}

public enum ConflictResolution
{
    KeepLocal,
    KeepRemote,
    KeepBoth,
    Merge
}
