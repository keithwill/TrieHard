using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrieHard.PrefixLookup;

namespace TrieHard.Benchmarks
{

    public static class PrefixLookupTestValues
    {

        /// <summary>
        ///A million sequential key value pairs with both key and value being the string representation of the sequence number
        /// </summary>
        public static readonly KeyValue<string>[] SequentialStrings;

        /// <summary>
        /// A million sequential UUID key value pairs, with the key being the textual representation of the UUID. These are not
        /// standards compliant GUIDs, instead using 
        /// </summary>
        public static readonly KeyValue<Guid>[] SequentialUuids;

        /// <summary>
        /// A thousand common English words as both key and value
        /// </summary>
        public static readonly KeyValue<string>[] EnglishWords;

        static PrefixLookupTestValues()
        {
            SequentialStrings = new KeyValue<string>[1_000_000];
            for(int i = 0; i < SequentialStrings.Length; i++)
            {
                var key = i.ToString();
                SequentialStrings[i] = new KeyValue<string>(key, key);
            }

            // No built in way to generate these, and I am not really interested in matching
            // a particular implementation, so I'm just faking something similar.
            // This is just being provided to show how longer keys with an ordered prefix might
            // perform compared to normal words or sequential integers
            SequentialUuids = new KeyValue<Guid>[1_000_000];
            Span<byte> guidSpan = stackalloc byte[16];
            Span<byte> guidTimePortion = guidSpan.Slice(0, 8);
            DateTime arbitraryDate = new DateTime(2000, 1, 1);
            Random random = new Random(2627272);
            random.NextBytes(guidSpan);

            for (int i = 0; i < SequentialUuids.Length; i++)
            {
                arbitraryDate = arbitraryDate.AddMilliseconds(random.Next(1000 * 60 * 5)); // Increment by up to 5 minutes
                BitConverter.TryWriteBytes(guidTimePortion, arbitraryDate.ToBinary());
                var guid = new Guid(guidSpan);
                SequentialUuids[i] = new KeyValue<Guid>(guid.ToString(), guid);
            }

            EnglishWords = CommonWords.English.Select(x => new KeyValue<string>(x, x)).ToArray();
        }


    }

}
