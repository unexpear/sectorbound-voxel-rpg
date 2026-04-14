using Sectorbound.Engine;

namespace Sectorbound.Client;

internal static class ActorScaleCatalog
{
    public static ActorScaleDef Player { get; } = new(1.8f, 0.6f, 0.4f);

    private static readonly IReadOnlyDictionary<string, ActorScaleDef> NpcScales =
        new Dictionary<string, ActorScaleDef>(StringComparer.OrdinalIgnoreCase)
        {
            ["settler"] = new(1.72f, 0.56f, 0.42f),
            ["merchant"] = new(1.75f, 0.6f, 0.45f),
            ["warden"] = new(1.95f, 0.72f, 0.54f)
        };

    public static ActorScaleDef GetPlayerScale() => Player;

    public static ActorScaleDef GetEnemyScale(EnemyDef enemy) => enemy.Scale;

    public static ActorScaleDef GetBossScale(BossDef boss) => boss.Scale;

    public static ActorScaleDef GetNpcScale(string? archetypeId = null)
    {
        if (!string.IsNullOrWhiteSpace(archetypeId) && NpcScales.TryGetValue(archetypeId, out var scale))
        {
            return scale;
        }

        return NpcScales["settler"];
    }
}
