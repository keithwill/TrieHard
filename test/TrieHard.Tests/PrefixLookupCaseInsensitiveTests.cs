using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrieHard.PrefixLookup;

namespace TrieHard.Tests
{
    public class PrefixLookupCaseInsensitiveTests
    {

        private const string lowerCaseKey = "testkey";
        private const string mixedCaseKey = "TestKey";
        private TestRecord lowerCaseTestRecord = new TestRecord(lowerCaseKey);
        private TestRecord mixedCaseRecord = new TestRecord(mixedCaseKey);

        [Test]
        public void PrefixLookup_Set_HonorsKeyCase()
        {            
            PrefixLookup<TestRecord> lookup = new PrefixLookup<TestRecord>(caseSensitive: false);
            lookup[lowerCaseKey] = lowerCaseTestRecord;
            lookup[mixedCaseKey] = lowerCaseTestRecord;

            Assert.That( 
                lookup.Count, Is.EqualTo(1), 
                message: "Case insensitive lookup stored two records for keys that only differed by case. They should have been treated as the same key"
            );
        }

        [Test]
        public void PrefixLookup_Get_HonorsKeyCase()
        {
            Assume.That(PrefixLookup_Set_HonorsKeyCase, Throws.Nothing);

            PrefixLookup<TestRecord> lookup = new PrefixLookup<TestRecord>(caseSensitive: false);
            lookup[mixedCaseKey] = mixedCaseRecord;

            var mixedCaseGet = lookup[mixedCaseKey];
            var lowerCaseGet = lookup[lowerCaseKey];

            Assert.That(mixedCaseGet, Is.SameAs(mixedCaseRecord), "Case insensitve lookup failed to retrieve the expected record with the exact key used to store it");
            Assert.That(lowerCaseGet, Is.SameAs(mixedCaseRecord), "Case insensitve lookup failed to retrieve the expected record when using a key that only differed by case from the one used to store the record");
        }

    }
}
