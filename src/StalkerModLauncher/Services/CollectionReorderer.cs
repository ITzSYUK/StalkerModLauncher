using System.Collections.ObjectModel;

namespace StalkerModLauncher.Services;

public static class CollectionReorderer
{
    public static bool MoveToInsertionIndex<T>(
        ObservableCollection<T> collection,
        T item,
        int insertionIndex)
    {
        var oldIndex = collection.IndexOf(item);
        if (oldIndex < 0 || insertionIndex < 0 || insertionIndex > collection.Count)
        {
            return false;
        }

        var newIndex = insertionIndex > oldIndex ? insertionIndex - 1 : insertionIndex;
        if (newIndex == oldIndex || newIndex < 0 || newIndex >= collection.Count)
        {
            return false;
        }

        collection.Move(oldIndex, newIndex);
        return true;
    }
}
