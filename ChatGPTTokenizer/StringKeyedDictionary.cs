using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ChatGPTTokenizer {
    public class StringKeyedDictionary<TValue> : IDisposable {
        private sealed class BufferedComparer : IEqualityComparer<(int Index, int Length)> {
            private IntPtr buffer;
            private int offset;
            private int length;

            public BufferedComparer(int capacity) {
                buffer = Marshal.AllocHGlobal(capacity * sizeof(char));
                offset = 0;
                length = capacity;
            }

            public void Free() {
                Marshal.FreeHGlobal(buffer);
            }

            unsafe public int Write(ReadOnlySpan<char> key) {
                if (offset + key.Length > length) {
                    length = Math.Max(length * 2, offset + key.Length);
                    buffer = Marshal.ReAllocHGlobal(buffer, length * sizeof(char));
                }
                key.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef<char>((char*)buffer + offset), key.Length));
                return offset;
            }

            public void Increase(int length) => offset += length;

            unsafe public bool Equals((int Index, int Length) x, (int Index, int Length) y) {
                var span1 = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<char>((char*)buffer + x.Index), x.Length);
                var span2 = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<char>((char*)buffer + y.Index), y.Length);
                return span1.SequenceEqual(span2);
            }

            unsafe public int GetHashCode([DisallowNull] (int Index, int Length) x) {
                var span = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<char>((char*)buffer + x.Index), x.Length);
                return string.GetHashCode(span);
            }
        }

        private BufferedComparer comparer;
        private readonly Dictionary<(int Index, int Length), TValue> dict;

        public StringKeyedDictionary() : this(16) { }

        public StringKeyedDictionary(int capacity) {
            comparer = new BufferedComparer(capacity);
            dict = new Dictionary<(int Index, int Length), TValue>(capacity, comparer);
        }

        public bool Add(ReadOnlySpan<char> key, TValue value) {
            int index = comparer.Write(key);
            if (dict.TryAdd((index, key.Length), value)) {
                comparer.Increase(key.Length);
                return true;
            }
            return false;
        }

        public bool TryGetValue(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out TValue value) {
            int index = comparer.Write(key);
            return dict.TryGetValue((index, key.Length), out value);
        }

        protected virtual void Dispose(bool disposing) {
            if (comparer != null) {
                comparer.Free();
                comparer = null!;
            }
        }

        ~StringKeyedDictionary() {
            Dispose(disposing: false);
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
