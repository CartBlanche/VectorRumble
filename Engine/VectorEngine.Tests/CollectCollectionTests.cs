using VectorEngine.Collections;
using Xunit;

namespace VectorEngine.Tests;

public class CollectCollectionTests
{
    [Fact]
    public void Collect_removes_only_garbage_and_keeps_order()
    {
        var c = new CollectCollection<string>();
        foreach (var s in new[] { "a", "b", "c", "d", "e", "f" }) c.Add(s);
        c.Garbage.Add("b");
        c.Garbage.Add("e");
        c.Collect();
        Assert.Equal(new[] { "a", "c", "d", "f" }, c);
        Assert.Empty(c.Garbage);
    }

    [Fact]
    public void Collect_handles_mostly_garbage_and_unknown_items()
    {
        var c = new CollectCollection<int>();
        for (int i = 0; i < 6; i++) c.Add(i);
        foreach (var i in new[] { 0, 1, 2, 3, 4, 99 }) c.Garbage.Add(i);
        c.Collect();
        Assert.Equal(new[] { 5 }, c);
    }

    [Fact]
    public void Collect_with_no_garbage_changes_nothing()
    {
        var c = new CollectCollection<int> { 1, 2, 3 };
        c.Collect();
        Assert.Equal(new[] { 1, 2, 3 }, c);
    }
}
