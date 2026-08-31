using NUnit.Framework;

public sealed class ReferenceTransitionPlanExecutor244Tests
{
    [Test]
    public void ExecutionResultCarriesRequestAndWriterInvocationContract()
    {
        var result = new ReferenceTransitionPlanExecutionResult243(
            42UL, 7, ReferenceTransitionPlanExecutionDisposition243.Submitted, true);

        Assert.AreEqual(42UL, result.RequestId);
        Assert.AreEqual(7, result.EntityInstanceId);
        Assert.AreEqual(ReferenceTransitionPlanExecutionDisposition243.Submitted, result.Disposition);
        Assert.IsTrue(result.WriterInvoked);
    }
}
