using System.Threading;
using TrieHard.Abstractions;
using TrieHard.Alternatives.List;
using TrieHard.Collections;

namespace TrieHard.Tests
{
    public class RadixTreeConcurrencyTests : PrefixLookupConcurrencyTests<RadixTree<string>> { }
    public class UnsafeTrieConcurrencyTests : PrefixLookupConcurrencyTests<RadixTree<string>> { }

    // These lookups fail during reads when a concurrent write occurs.
    //public class ListPrefixLookupConcurrencyTests : PrefixLookupConcurrencyTests<ListPrefixLookup<string>> { }
    //public class SimpleTrieConcurrencyTests : PrefixLookupConcurrencyTests<SimpleTrie<string>> { }


    public abstract class PrefixLookupConcurrencyTests<T> where T : IPrefixLookup<string>
    {
        private const int iterationSize = 1000;

        public record class TestEntity(string Key) {}

        private Random rng = new(Environment.TickCount);

        /// <summary>
        /// A brutish way to confirm that the results returning from the PrefixLookup look sane,
        /// with a single writer and many readers.
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task ConcurrentWrite_SearchResults_AreStable()
        {
            Dictionary<string, TestEntity> expectedLookup = new();
            List<TestEntity> expectedValues = new();

            for (int i = 0; i < iterationSize; i++)
            {
                var testEntity = new TestEntity(Key: i.ToString());
                expectedValues.Add(testEntity);
                expectedLookup.Add(i.ToString(), testEntity);
            }

            var iterationCount = 0;


            using CancellationTokenSource timeoutCancellation = new(3000);
            var readers = new Task[10];
            while (!timeoutCancellation.IsCancellationRequested)
            {
                using CancellationTokenSource iterationCancellation = new();
                var lookup = T.Create<TestEntity>();

                for (var i = 0; i < readers.Length; i++)
                {
                    readers[i] = Task.Run(() => ReadAndVerifyPrefixValues(lookup, expectedLookup, iterationCancellation.Token));
                }
                var writeTask = Task.Run(() => WriteValues(lookup, expectedValues, iterationCancellation, timeoutCancellation.Token));

                try
                {
                    await writeTask;
                    await Task.WhenAll(readers);
                    WriteGaps(lookup, expectedValues, timeoutCancellation.Token);
                    ReadAndVerifyPrefixCounts(lookup, expectedValues, timeoutCancellation.Token);
                    iterationCount++;
                }
                catch(OperationCanceledException) { }
            }
            Console.WriteLine($"Completed: {iterationCount} prefix validations ({iterationSize * iterationCount} items)");

        }

        /// <summary>
        /// A common problem with concurrent updates to a Trie is that the index of the children
        /// gets shifted as new values are inserted. This usually tears reads and update locations,
        /// sometimes causing key value pairs to be dropped or not returned from a query. After the concurrent
        /// updates are done to the the lookup of this test, we'll compare the counts to what they should
        /// be for prefix searches, just to see if any records were corrupted.
        /// </summary>
        private void ReadAndVerifyPrefixCounts(IPrefixLookup<TestEntity?> lookup, List<TestEntity> expectedValues, CancellationToken timeout)
        {
            for (int i = 0; i < iterationSize; i++)
            {
                if (timeout.IsCancellationRequested) return;
                var prefix = i.ToString();
                var expectedCount = expectedValues.Where(x => x.Key.StartsWith(prefix)).Count();
                var actualCount = lookup.SearchValues(prefix).Count();
                try
                {
                    Assert.That(actualCount, Is.EqualTo(expectedCount));

                }
                catch (Exception)
                {
                    var searchResults2 = lookup.SearchValues(prefix).ToArray();
                    var searchResults3 = lookup.Search(prefix).ToArray();
                    throw;
                }
            }
        }

        /// <summary>
        /// Until the writer signals completion, enter a loop to search for random prefix values in the iteration range
        /// and ensure that any key value pair match returns keys and values that should be in the resulting set.
        /// We won't know if the writer actually wrote those values, but if they are torn or corrupt they will usually
        /// not have a key that matches the value, or will have keys that shouldn't exist at all.
        /// </summary>
        private void ReadAndVerifyPrefixValues(IPrefixLookup<TestEntity?> lookup, Dictionary<string, TestEntity> expectedLookup, CancellationToken timeout)
        {

            while (!timeout.IsCancellationRequested)
            {
                var nextKey = rng.Next(0, iterationSize).ToString();
                int resultCount = 0;
                foreach (var kvp in lookup.Search(nextKey))
                {
                    resultCount++;
                    if (timeout.IsCancellationRequested) { return; }
                    Assert.That(kvp.Value, Is.Not.Null);
                    var key = kvp.Key;
                    Assert.That(expectedLookup, Contains.Key(key));
                    StringAssert.StartsWith(nextKey, key);
                    var expectedValueKey = expectedLookup[key].Key;
                    Assert.That(expectedValueKey, Is.EqualTo(kvp.Value.Key));
                }
            }
        }

        private void WriteGaps(IPrefixLookup<TestEntity?> lookup, List<TestEntity> expectedValues, CancellationToken timeout)
        {
            for (int i = 0; i < iterationSize; i++)
            {
                if (timeout.IsCancellationRequested) return;
                var nextValue = expectedValues[i];
                lookup[i.ToString()] = nextValue;
            }
        }

        /// <summary>
        /// Write randomly selected elements from the expected values into the trie, then
        /// signal that writing is done.
        /// </summary>
        private void WriteValues(IPrefixLookup<TestEntity?> lookup, List<TestEntity> expectedValues, CancellationTokenSource iterationCancellation, CancellationToken timeout)
        {
            // Write keys at random
            for (int i = 0; i < iterationSize * 3; i++)
            {
                if (timeout.IsCancellationRequested)
                {
                    iterationCancellation.Cancel();
                    return;
                };
                var next = rng.Next(0, iterationSize);
                var nextValue = expectedValues[next];
                lookup[next.ToString()] = nextValue;
                Thread.Yield();
            }

            iterationCancellation.Cancel();
        }



    }
}
