using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ChatGPTTokenizer {
    [DebuggerDisplay("{ToString(),raw}")]
    unsafe internal readonly struct NativeString : IEquatable<NativeString> {
        private readonly char* ptr;
        private readonly int length;

        public char* Ptr => ptr;

        public int Length => length;

        public ReadOnlySpan<char> Span {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<char>(ptr), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeString(IntPtr ptr, int length) {
            this.ptr = (char*)ptr;
            this.length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeString(char* ptr, int length) {
            this.ptr = ptr;
            this.length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(NativeString other) {
            return Span.SequenceEqual(other.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals([NotNullWhen(true)] object? obj) {
            return obj is NativeString other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() {
            return string.GetHashCode(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<char>(ptr), length));
        }

        public override string? ToString() {
            if (ptr == null) return null;
            if (length == 0) return "";
            return new string(ptr, 0, length);
        }

        public static bool operator ==(NativeString a, NativeString b) => a.Equals(b);
        public static bool operator !=(NativeString a, NativeString b) => !a.Equals(b);
    }
}
