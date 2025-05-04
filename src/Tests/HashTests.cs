[TestFixture]
public class HashTests
{
    [Test]
    public void Compute()
    {
        // Arrange
        var input = "test";
        var expectedHash = "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3";

        var result = Hash.Compute(input);

        AreEqual(expectedHash, result);
    }
}