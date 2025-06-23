[TestFixture]
public class FilePairTests
{
    [Test]
    public void SetExpiry_ShouldSetMinFileDate_WhenExpiryIsNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            // Arrange
            var filePair = new FilePair(path, "");
            var expectedDate = FileEx.MinFileDate;

            // Act
            filePair.SetExpiry(null);

            // Assert
            var actualDate = File.GetLastWriteTimeUtc(path);
            AreEqual(expectedDate, actualDate);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void SetExpiry_ShouldSetExpiryDate_WhenExpiryIsProvided()
    {
        // Arrange
        var path = Path.GetTempFileName();
        try
        {
            var filePair = new FilePair(path, "");
            var expiryDate = DateTimeOffset.UtcNow.AddDays(1);

            // Act
            filePair.SetExpiry(expiryDate);

            // Assert
            var actualDate = File.GetLastWriteTimeUtc(path);
            AreEqual(expiryDate.UtcDateTime, actualDate);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void SetExpiry_ShouldSetMinFileDate_WhenExpiryIsBeforeMinFileDate()
    {
        // Arrange
        var path = Path.GetTempFileName();
        try
        {
            var filePair = new FilePair(path, "");

            // Act
            filePair.SetExpiry(DateTimeOffset.MinValue);

            // Assert
            var actualDate = File.GetLastWriteTimeUtc(path);
            AreEqual(FileEx.MinFileDate, actualDate);
        }
        finally
        {
            File.Delete(path);
        }
    }
}