namespace DurableOne
{
    public class StartImageParams
    {
        public string Arg1 { get; set; }

        public string Arg2 { get; set; }
        public string ResourceGroupName { get; internal set; }
        public string ContainerGroupName { get; internal set; }
        public string RegistryServer { get; internal set; }
        public string RegistryUsername { get; internal set; }
        public string RegistryPassword { get; internal set; }
        public string ContainerImage { get; internal set; }
        public double CpuCoreCount { get; internal set; }
        public double MemorySizeInGB { get; internal set; }
        public string TransactionId { get; internal set; }
    }
}