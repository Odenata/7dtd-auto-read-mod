using Xunit;

// ModConfig.Instance and GameManager.Instance are static; parallel tests race on SetConfig.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
