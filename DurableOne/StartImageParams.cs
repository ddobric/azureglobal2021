namespace DurableOne
{
    public class StartImageParams
    {
        public string Arg1 { get; set; }

        public string Arg2 { get; set; }
        public string ResourceGroupName { get;  set; }
        public string ContainerGroupName { get;  set; }
        public string RegistryServer { get;  set; }
        public string RegistryUsername { get;  set; }
        public string RegistryPassword { get;  set; }
        public string ContainerImage { get;  set; }
        public int CpuCoreCount { get;  set; }
        public int MemorySizeInGB { get;  set; }
        public string TransactionId { get;  set; }
        public string ClientId { get; set; }
        public string Secret { get; set; }
        public string SubscriptionId { get;  set; }
        public string TenantId { get; set; }
    }
}