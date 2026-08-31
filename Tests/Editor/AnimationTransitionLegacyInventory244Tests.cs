using NUnit.Framework;

public sealed class AnimationTransitionLegacyInventory244Tests
{
    [Test]
    public void InventoryBlocksWhileDirectCallersRemain()
    {
        var inventory = AnimationTransitionLegacyInventory244.Evaluate(3, 0, true);

        Assert.IsFalse(inventory.IsReady);
        Assert.AreEqual("direct-play-callers-remain", inventory.BlockReason);
    }

    [Test]
    public void InventoryRequiresSingleWriterAndNoLegacyReaders()
    {
        var inventory = AnimationTransitionLegacyInventory244.Evaluate(0, 0, true);

        Assert.IsTrue(inventory.IsReady);
        Assert.IsEmpty(inventory.BlockReason);
    }

    [Test]
    public void InventoryBlocksWhileLegacyReadersRemain()
    {
        var inventory = AnimationTransitionLegacyInventory244.Evaluate(0, 2, true);

        Assert.IsFalse(inventory.IsReady);
        Assert.AreEqual("legacy-readers-remain", inventory.BlockReason);
    }

    [Test]
    public void InventoryBlocksWhenSingleWriterIsNotProven()
    {
        var inventory = AnimationTransitionLegacyInventory244.Evaluate(0, 0, false);

        Assert.IsFalse(inventory.IsReady);
        Assert.AreEqual("single-writer-not-proven", inventory.BlockReason);
    }
}
