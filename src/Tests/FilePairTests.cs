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

    [Test]
    public void SetExpiry_WithNegativeMaxAgeResultingInPastDate_ShouldNotThrow()
    {
        // Regression test for https://github.com/SimonCropp/Replicant/issues/176
        // When HTTP response has negative max-age (e.g., max-age=-1), the calculated
        // expiry will be in the past. This should not throw ArgumentOutOfRangeException.
        var path = Path.GetTempFileName();
        try
        {
            var filePair = new FilePair(path, "");

            // Simulate expiry calculated from negative max-age: now.Add(TimeSpan.FromSeconds(-1))
            var pastExpiry = DateTimeOffset.UtcNow.AddSeconds(-1);

            // Should not throw
            filePair.SetExpiry(pastExpiry);

            // Past dates are still valid, just means content is already expired
            var actualDate = File.GetLastWriteTimeUtc(path);
            AreEqual(pastExpiry.UtcDateTime, actualDate);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void SetExpiry_WithDateBeforeWin32Epoch_ShouldNotThrow()
    {
        // Regression test for https://github.com/SimonCropp/Replicant/issues/176
        // Dates before 1601-01-01 (Win32 FileTime epoch) would throw
        // "Not a valid Win32 FileTime" if not handled properly.
        var path = Path.GetTempFileName();
        try
        {
            var filePair = new FilePair(path, "");

            // Date before Win32 FileTime epoch (1601-01-01)
            var invalidDate = new DateTimeOffset(1600, 1, 1, 0, 0, 0, TimeSpan.Zero);

            // Should not throw - should fall back to MinFileDate
            filePair.SetExpiry(invalidDate);

            var actualDate = File.GetLastWriteTimeUtc(path);
            AreEqual(FileEx.MinFileDate, actualDate);
        }
        finally
        {
            File.Delete(path);
        }
    }
}