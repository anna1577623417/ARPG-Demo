using UnityEngine;

public static class EnemyRuntimeDiag
{
    public static bool IsEnabled => GameMainDebugSettings.CombatHit
                                     || GameMainDebugSettings.IntentArbitration
                                     || GameMainDebugSettings.InterruptFlow;

    public static void LogReactionState(Enemy enemy, string state, string eventName)
    {
        if ((!IsEnabled && !GameMainDebugSettings.ReactionDirection2206Log)
            || enemy == null)
        {
            return;
        }

        Debug.Log(
            $"[Enemy] FSM event={eventName} state={state} entity={enemy.name} " +
            $"instanceId={enemy.GetInstanceID()} log=220.6",
            enemy);
    }

    public static void LogState(Enemy enemy, string state, string eventName)
    {
        if (!IsEnabled || enemy == null)
        {
            return;
        }

        Debug.Log(
            $"[Enemy] FSM event={eventName} state={state} entity={enemy.name} " +
            $"instanceId={enemy.GetInstanceID()}",
            enemy);
    }

    public static void LogArbitration(
        Enemy enemy,
        GameplayIntentKind kind,
        string phase,
        string result,
        SkillRouteRuntime route = null,
        string reason = null)
    {
        if (!IsEnabled || enemy == null)
        {
            return;
        }

        Debug.Log(
            $"[Enemy] IntentArb phase={phase} kind={kind} result={result} " +
            $"route={route?.Definition?.name ?? "-"} reason={reason ?? "-"} " +
            $"state={enemy.StateManager?.Current?.StateId ?? "-"} instanceId={enemy.GetInstanceID()}",
            enemy);
    }
}
