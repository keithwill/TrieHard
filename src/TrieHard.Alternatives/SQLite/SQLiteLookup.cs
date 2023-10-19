using Microsoft.Data.Sqlite;
using System.Collections;
using TrieHard.Abstractions;
using TrieHard.PrefixLookup;

namespace TrieHard.Alternatives.SQLite
{

    /// <summary>
    /// An IPrefixLookup implementation that stores key value pairs in a simple table
    /// inside of SQLite.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SQLiteLookup<T> : IPrefixLookup<T?>, IDisposable
    {
        public static bool IsImmutable => true;
        public static Concurrency ThreadSafety => Concurrency.None;

        public static bool IsSorted => true;

        private SqliteConnection connection;
        private T?[] values = Array.Empty<T>();
        private string connectionString;
        private bool isDisposed = false;
        private SqliteCommand? searchCommand;
        private SqliteParameter? searchKeyParamter;

        private SqliteCommand? getCommand;
        private SqliteParameter? getKeyParameter;

        public SQLiteLookup() 
        {
            connectionString = $"Data Source=:memory:";
            connection = new SqliteConnection(connectionString);
            connection.Open();
        }

        public T? this[string key] {
            get => Get(key);
            set => throw new NotImplementedException(); 
        }


        public int Count => values.Length;

        public static IPrefixLookup<TValue?> Create<TValue>(IEnumerable<KeyValue<TValue?>> source)
        {
            var lookup = new SQLiteLookup<TValue>();
            using var cmd = new SqliteCommand("CREATE TABLE lookup ( Key PRIMARY KEY, ValueIndex ) WITHOUT ROWID", lookup.connection);
            cmd.ExecuteNonQuery();
            var kvps = source.ToArray();
            lookup.values = new TValue[kvps.Length];

            using var tx = lookup.connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
            using var insertCmd = new SqliteCommand("INSERT INTO lookup (Key, ValueIndex) VALUES (@Key, @ValueIndex)", lookup.connection, tx);
            var keyParam = insertCmd.Parameters.Add("@Key", SqliteType.Text);
            var valueParam = insertCmd.Parameters.Add("@ValueIndex", SqliteType.Integer);
            for(int i = 0; i < kvps.Length; i++)
            {
                var kvp = kvps[i];
                lookup.values[i] = kvp.Value;
                valueParam.Value = i;
                keyParam.Value = kvp.Key;
                insertCmd.ExecuteNonQuery();
            }
            tx.Commit();
            lookup.searchCommand = new SqliteCommand("SELECT Key, ValueIndex FROM lookup WHERE Key like @Key ORDER BY Key", lookup.connection);
            lookup.searchKeyParamter = lookup.searchCommand.Parameters.Add("@Key", SqliteType.Text);

            lookup.getCommand = new SqliteCommand("SELECT ValueIndex FROM lookup WHERE Key = @Key", lookup.connection);
            lookup.getKeyParameter = lookup.getCommand.Parameters.Add("@Key", SqliteType.Text);
            return lookup;
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                connection.Dispose();
                isDisposed = true;
                searchCommand?.Dispose();
                getCommand?.Dispose();
            }
        }

        private static class Ord
        {
            public const int Key = 0;
            public const int ValueIndex = 1;
        }

        public IEnumerator<KeyValue<T?>> GetEnumerator()
        {
            return Search("").GetEnumerator();
        }

        public T? Get(string key)
        {
            getKeyParameter!.Value = key;
            var valueIndex = getCommand!.ExecuteScalar();
            if (valueIndex == null)
            {
                return default!;
            }
            return values[(long)valueIndex];
        }

        public IEnumerable<KeyValue<T?>> Search(string keyPrefix)
        {
            searchKeyParamter!.Value = $"{keyPrefix}%";
            using var reader = searchCommand!.ExecuteReader();
            while(reader.Read())
            {
                yield return new KeyValue<T?>(reader.GetString(Ord.Key), values[reader.GetInt32(Ord.ValueIndex)]);
            }
        }

        public IEnumerable<T?> SearchValues(string keyPrefix)
        {
            searchKeyParamter!.Value = $"{keyPrefix}%";
            using var reader = searchCommand!.ExecuteReader();
            while (reader.Read())
            {
                yield return values[reader.GetInt32(Ord.ValueIndex)];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public static IPrefixLookup<T?> Create<TValue>()
        {
            throw new NotImplementedException();
        }
    }
}
