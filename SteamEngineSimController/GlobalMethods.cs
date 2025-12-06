global using static SteamEngineSimController.GlobalMethods;
using System.Diagnostics;

namespace SteamEngineSimController;
public static class GlobalMethods {

    [DebuggerHidden]
    public static void Assert(bool c, string? message = null) {
        if (!c) {
            throw new AssertionFailedException(message != null ? $"An assertion has failed: {message}" : "An assertion has failed!");
        }
    }

    [DebuggerHidden]
    public static void AssertImplies(bool a, bool b, string? message = null) => Assert(!a || b, message);
}

[Serializable]
internal class AssertionFailedException : Exception {
    public AssertionFailedException() {
    }

    public AssertionFailedException(string? message) : base(message) {
    }

    public AssertionFailedException(string? message, Exception? innerException) : base(message, innerException) {
    }
}