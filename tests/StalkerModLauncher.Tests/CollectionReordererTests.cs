using System.Collections.ObjectModel;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class CollectionReordererTests
{
    [Theory]
    [InlineData(0, 3, new[] { "B", "C", "A", "D" })]
    [InlineData(2, 0, new[] { "C", "A", "B", "D" })]
    [InlineData(1, 4, new[] { "A", "C", "D", "B" })]
    public void MoveToInsertionIndex_MovesToRequestedSlot(
        int sourceIndex,
        int insertionIndex,
        string[] expected)
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
    public void MoveToInsertionIndex_DoesNothingForEquivalentSlot()
    {
        var collection = new ObservableCollection<string>(["A", "B", "C"]);

        var moved = CollectionReorderer.MoveToInsertionIndex(collection, collection[1], 2);

        Assert.False(moved);
        Assert.Equal(["A", "B", "C"], collection);
    }
}
