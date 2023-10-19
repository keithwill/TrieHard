using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrieHard.PrefixLookup
{
    public record struct KeyValue<T>
    {

        public KeyValue(string key, T? value)
        {
            this.key = key;
            this.value = value;
        }

        public KeyValue(ReadOnlyMemory<byte> keyUtf8, T? value)
        {
            this.keyUtf8 = keyUtf8;
            this.value = value;
        }

        public KeyValue<T> WithValue(T? value)
        {
            return new KeyValue<T>() { key = this.key, keyUtf8 = this.keyUtf8, value = value};
        }

        public string Key => key ??= System.Text.Encoding.UTF8.GetString(keyUtf8.Span);
        public ReadOnlyMemory<byte> KeyUtf8 => keyUtf8.Length > 0 ? keyUtf8 : keyUtf8 = System.Text.Encoding.UTF8.GetBytes(key).AsMemory();
        public T? Value => value;
        
        private string key;
        private ReadOnlyMemory<byte> keyUtf8;
        private T? value;
    }
}
