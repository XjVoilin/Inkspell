namespace Game.Aot
{
    /// <summary>启动期解析后的资源地址值对象，空地址表示不使用远程资源源。</summary>
    public readonly struct CDNEndpoints
    {
        public readonly string MainURL;

        public CDNEndpoints(string mainURL)
        {
            MainURL = mainURL;
        }

        public static readonly CDNEndpoints Empty = new(string.Empty);
    }
}
