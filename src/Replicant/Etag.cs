namespace Replicant
{
    public readonly struct Etag
    {
        public string Value { get; }
        public bool Weak { get; }

        public Etag(string value, bool weak)
        {
            Value = value;
            Weak = weak;
        }
    }
}