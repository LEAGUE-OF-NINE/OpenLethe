// Shim for Il2CppInterop types referenced by client-extracted packet definitions.
// The Limbus client is IL2CPP Unity; these are its array types. Server-side we need them
// only to exist and to serialize as JSON arrays, which List<T> already does.
// Verified: 78 uses of Il2CppReferenceArray<T>, 56 of Il2CppStructArray<T>, and these are
// the ONLY two Unity runtime types the packet files reference.
// ponytail: List<T> subclass, not a faithful array wrapper. Upgrade only if a packet ever
// needs fixed-length or index-assignment semantics the wire format actually depends on.

using System.Collections.Generic;

public class Il2CppReferenceArray<T> : List<T>
{
    public Il2CppReferenceArray() { }
    public Il2CppReferenceArray(IEnumerable<T> items) : base(items) { }
}

public class Il2CppStructArray<T> : List<T> where T : struct
{
    public Il2CppStructArray() { }
    public Il2CppStructArray(IEnumerable<T> items) : base(items) { }
}
