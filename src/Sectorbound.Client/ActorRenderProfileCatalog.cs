using System.Numerics;
using Sectorbound.Engine;

namespace Sectorbound.Client;

internal readonly record struct ActorRenderStanceOffsets(Vector3 Idle, Vector3 Combat)
{
    public static ActorRenderStanceOffsets None { get; } = new(Vector3.Zero, Vector3.Zero);

    public Vector3 GetOffset(bool useCombatStance) => useCombatStance ? Combat : Idle;
}

internal readonly record struct ActorRenderProfile(
    ActorScaleDef Scale,
    string? RelativeAssetPath,
    Vector3 PivotOffset,
    ActorRenderStanceOffsets StanceOffsets)
{
    public Vector3 GetStanceOffset(bool useCombatStance) => StanceOffsets.GetOffset(useCombatStance);
}

internal static class ActorRenderProfileCatalog
{
    private readonly record struct ActorRenderMetadata(
        string? RelativeAssetPath,
        Vector3 PivotOffset,
        ActorRenderStanceOffsets StanceOffsets);

    private static readonly ActorRenderMetadata PlayerMetadata = new(
        RelativeAssetPath: null,
        PivotOffset: Vector3.Zero,
        StanceOffsets: new ActorRenderStanceOffsets(
            Idle: Vector3.Zero,
            Combat: new Vector3(0f, 0f, -0.04f)));

    private static readonly IReadOnlyDictionary<string, ActorRenderMetadata> NpcMetadata =
        new Dictionary<string, ActorRenderMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["settler"] = CreateVoxelMetadata("Npcs", "settler"),
            ["merchant"] = CreateVoxelMetadata("Npcs", "merchant"),
            ["warden"] = CreateVoxelMetadata("Npcs", "warden")
        };

    private static readonly IReadOnlyDictionary<string, ActorRenderMetadata> EnemyMetadata =
        new Dictionary<string, ActorRenderMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["burrowling"] = CreateVoxelMetadata("Enemies", "burrowling"),
            ["cinderbat"] = CreateVoxelMetadata("Enemies", "cinderbat"),
            ["shardling"] = CreateVoxelMetadata("Enemies", "shardling")
        };

    private static readonly IReadOnlyDictionary<string, ActorRenderMetadata> BossMetadata =
        new Dictionary<string, ActorRenderMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["void-wyrm"] = CreateVoxelMetadata("Bosses", "void-wyrm")
        };

    public static ActorRenderProfile GetPlayerProfile(CharacterSelection selection)
    {
        var normalizedSelection = CharacterCatalog.Normalize(selection.BodyId, selection.HairId);
        var metadata = PlayerMetadata with
        {
            RelativeAssetPath = CharacterCatalog.GetRelativeAssetPath(normalizedSelection)
        };

        return CreateProfile(ActorScaleCatalog.GetPlayerScale(), metadata);
    }

    public static ActorRenderProfile GetNpcProfile(string? archetypeId = null)
    {
        var resolvedArchetypeId = ResolveNpcArchetypeId(archetypeId);
        var metadata = NpcMetadata.TryGetValue(resolvedArchetypeId, out var profile)
            ? profile
            : CreateVoxelMetadata("Npcs", resolvedArchetypeId);

        return CreateProfile(ActorScaleCatalog.GetNpcScale(resolvedArchetypeId), metadata);
    }

    public static ActorRenderProfile GetEnemyProfile(EnemyDef enemy)
    {
        var metadata = EnemyMetadata.TryGetValue(enemy.Id, out var profile)
            ? profile
            : CreateVoxelMetadata("Enemies", enemy.Id);

        return CreateProfile(ActorScaleCatalog.GetEnemyScale(enemy), metadata);
    }

    public static ActorRenderProfile GetBossProfile(BossDef boss)
    {
        var metadata = BossMetadata.TryGetValue(boss.Id, out var profile)
            ? profile
            : CreateVoxelMetadata("Bosses", boss.Id);

        return CreateProfile(ActorScaleCatalog.GetBossScale(boss), metadata);
    }

    private static ActorRenderProfile CreateProfile(ActorScaleDef scale, ActorRenderMetadata metadata) =>
        new(scale, metadata.RelativeAssetPath, metadata.PivotOffset, metadata.StanceOffsets);

    private static ActorRenderMetadata CreateVoxelMetadata(
        string folderName,
        string actorId,
        Vector3? pivotOffset = null,
        ActorRenderStanceOffsets? stanceOffsets = null) =>
        new(
            Path.Combine("Content", "Voxel", folderName, $"{actorId}.vox"),
            pivotOffset ?? Vector3.Zero,
            stanceOffsets ?? ActorRenderStanceOffsets.None);

    private static string ResolveNpcArchetypeId(string? archetypeId) =>
        string.IsNullOrWhiteSpace(archetypeId) ? "settler" : archetypeId.Trim();
}
