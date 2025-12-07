namespace SteamEngineSimController.MemoryHelpers;
internal class ReadOnlyMemoryLocation<T> : MemoryLocation<T> {
    public ReadOnlyMemoryLocation(nint handle, nint location) : base(handle, location) { }

    public ReadOnlyMemoryLocation(nint handle, nint location, int offset) : base(handle, location, offset) { }

    public override void SetValue(T newValue) {
        throw new InvalidOperationException();
    }
}
