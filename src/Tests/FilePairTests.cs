public class FilePairTests
{
    [Test]
    public async Task FromContentFile_WithJsonFile_CreatesSelfReferencingPair()
    {
        // FromContentFile expects a .bin content file and derives the .json meta path.
        // If given a .json file, both Content and Meta will point to the same file.
        var jsonPath = "/cache/hash_2021-03-22T095023_Setag.json";

        var pair = FilePair.FromContentFile(jsonPath);

        await Assert.That(pair.Content).IsEqualTo(jsonPath);
        await Assert.That(pair.Meta).IsEqualTo(jsonPath);
    }

    [Test]
    public async Task SetExpiry_ShouldSetMinFileDate_WhenExpiryIsNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            // Arrange
            var filePair = new FilePair(path, "");
            // Act
            filePair.SetExpiry(null);

            // Assert
            var actualDate = File.GetLastWriteTimeUtc(path);
            await Assert.That(FileEx.IsNoExpiry(actualDate)).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task SetExpiry_ShouldSetExpiryDate_WhenExpiryIsProvided()
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
            await Assert.That(actualDate).IsEqualTo(expiryDate.UtcDateTime);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task SetExpiry_ShouldSetMinFileDate_WhenExpiryIsBeforeMinFileDate()
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
            await Assert.That(FileEx.IsNoExpiry(actualDate)).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task SetExpiry_WithNegativeMaxAgeResultingInPastDate_ShouldNotThrow()
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
            await Assert.That(actualDate).IsEqualTo(pastExpiry.UtcDateTime);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task SetExpiry_WithDateBeforeWin32Epoch_ShouldNotThrow()
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
            await Assert.That(FileEx.IsNoExpiry(actualDate)).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }
}