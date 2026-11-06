namespace Mrbr.Cryptography.Benchmarks;

public sealed record SharingScenario(int SecretLength, int Threshold, int ShareCount)
{
    public override string ToString() => $"{SecretLength}B_{Threshold}of{ShareCount}";
}
