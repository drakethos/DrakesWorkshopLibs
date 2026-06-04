namespace DrakesWorkshopLibs.API;

public readonly struct TagBlockRule
{
    public TagBlockRule(string tagKey, CustomizeOperation blockedOperations)
    {
        TagKey = tagKey;
        BlockedOperations = blockedOperations;
    }

    public string TagKey { get; }
    public CustomizeOperation BlockedOperations { get; }
}
