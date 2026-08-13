using NUnit.Framework;
using UnityEngine;

public sealed class ContactPlacementMathTests
{
    [Test]
    public void WorldLocalRoundTrip_PreservesPoseUnderRotatedBasis()
    {
        var basisPosition = new Vector3(3f, 1f, -2f);
        var basisRotation = Quaternion.Euler(15f, 73f, -8f);
        var localPosition = new Vector3(0.4f, 1.2f, 0.8f);
        var localRotation = Quaternion.Euler(20f, -35f, 5f);

        ContactPlacementMath.ResolveWorld(
            basisPosition,
            basisRotation,
            localPosition,
            localRotation,
            out var worldPosition,
            out var worldRotation);
        ContactPlacementMath.ResolveLocal(
            basisPosition,
            basisRotation,
            worldPosition,
            worldRotation,
            out var roundTripPosition,
            out var roundTripRotation);

        Assert.AreEqual(localPosition.x, roundTripPosition.x, 0.0001f);
        Assert.AreEqual(localPosition.y, roundTripPosition.y, 0.0001f);
        Assert.AreEqual(localPosition.z, roundTripPosition.z, 0.0001f);
        Assert.Less(Quaternion.Angle(localRotation, roundTripRotation), 0.001f);
    }
}
