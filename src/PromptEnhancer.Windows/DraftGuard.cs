using System.Security.Cryptography;
using System.Text;

namespace PromptEnhancer.Windows;

internal sealed class DraftGuard
{
    private readonly byte[] original;
    private bool active = true;
    public DraftGuard(string originalText) => original = Hash(originalText);
    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
    public void Invalidate() => active = false;

    // Consume the permission before the write: a provider can mutate then throw.
    public bool TryWrite(bool sameDestination, string currentText, Action write)
    {
        if (!active || !sameDestination || !CryptographicOperations.FixedTimeEquals(original,Hash(currentText)))
        {
            active = false;
            return false;
        }
        active = false;
        write();
        return true;
    }
}

