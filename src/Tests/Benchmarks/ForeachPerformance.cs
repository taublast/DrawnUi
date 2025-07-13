using BenchmarkDotNet.Attributes;
using DrawnUi.Draw;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

public class ForeachPerformance
{
    private List<SkiaControl> myList;
    //private ImmutableArray<SkiaControl> myImmutableArray;

    [GlobalSetup]
    public void Setup()
    {
        myList = new List<SkiaControl>();
        var builder = ImmutableArray.CreateBuilder<SkiaControl>();
        for (int i = 0; i < 512; i++)
        {
            myList.Add(new SkiaControl());
            //builder.Add(new SkiaControl());
        }
        //myImmutableArray = builder.ToImmutable();
    }

    [Benchmark]
    public void ListWithSpan()
    {
        var asSpan = CollectionsMarshal.AsSpan(myList);
        foreach (var item in asSpan)
        {
            var temp = item; // Simulate some work
        }
    }

    [Benchmark]
    public void ImmutableArrayForeach()
    {
        var myImmutableArray = myList.ToImmutableArray();
        foreach (var item in myImmutableArray)
        {
            var temp = item; // Simulate some work
        }
    }
}

