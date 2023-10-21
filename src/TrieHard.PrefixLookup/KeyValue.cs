namespace TrieHard.Collections
{
    public record struct KeyValue<T>
    {

        public KeyValue(string key, T? value)
        {
            this.key = key;
            this.value = value;
            this.keyUtf8 = System.Text.Encoding.UTF8.GetBytes(key).AsMemory();
        }

        public KeyValue(ReadOnlyMemory<byte> keyUtf8, T? value)
        {
            this.keyUtf8 = keyUtf8;
            this.key = System.Text.Encoding.UTF8.GetString(keyUtf8.Span);
            this.value = value;
        }

        public KeyValue<T> WithValue(T? value)
        {
            return new KeyValue<T>() { key = this.key, keyUtf8 = this.keyUtf8, value = value};
        }

        public string Key => key;
        public ReadOnlyMemory<byte> KeyUtf8 => keyUtf8;
        public T? Value => value;
        
        private string key;
        private ReadOnlyMemory<byte> keyUtf8;
        private T? value;
    }
}
