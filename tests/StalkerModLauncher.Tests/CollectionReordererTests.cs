using System.Collections.ObjectModel;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class CollectionReordererTests
{
    [Theory]
    [InlineData(0, 3, "B", "C", "A", "D")]
    [InlineData(2, 0, "C", "A", "B", "D")]
    [InlineData(1, 4, "A", "C", "D", "B")]
    public void MoveToInsertionIndexMovesToRequestedSlot(
        int sourceIndex,
        int insertionIndex,
        params string[] expected)
    {
        var collection = new ObservableCollection<string>(["A", "B", "C", "D"]);

        var moved = CollectionReorderer.MoveToInsertionIndex(
            collection,
            collection[sourceIndex],
            insertionIndex);

        Assert.True(moved);
        Assert.Equal(expected, collection);
    }

    [Fact]
    public void MoveToInsertionIndexDoesNothingForEquivalentSlot()
    {
        var collection = new ObservableCollection<string>(["A", "B", "C"]);

        var moved = CollectionReorderer.MoveToInsertionIndex(collection, collection[1], 2);

        Assert.False(moved);
        Assert.Equal(["A", "B", "C"], collection);
    }
}
