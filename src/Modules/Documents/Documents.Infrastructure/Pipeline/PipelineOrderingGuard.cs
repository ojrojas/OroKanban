namespace Documents.Infrastructure.Pipeline;
public static class PipelineOrderingGuard { public static bool CanProceed(string stage, bool isSafe) => isSafe || stage == "Validation" || stage == "VirusScan"; }
