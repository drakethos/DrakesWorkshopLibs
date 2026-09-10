namespace DrakeModsLibs.API;

public interface IStackMergePolicy
{
    bool SeparateStacksEnabled { get; }
    bool SeparateStacksHardLock { get; }
}
