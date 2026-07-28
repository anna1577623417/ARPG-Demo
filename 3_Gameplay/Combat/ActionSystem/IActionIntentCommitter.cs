public interface IActionIntentCommitter
{
    bool TryCommitActionIntent(
        in GameplayIntent intent,
        in ArbitrationDecision decision,
        out string reason);
}
