using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ChatGPTTokenizer {
    [DebuggerDisplay("Count = {Count}")]
    unsafe internal sealed class Vocab : IReadOnlyDictionary<NativeString, int>, IDisposable {
        private readonly char* ptr;
        private int offset;
        private readonly int length;
        private readonly Dictionary<NativeString, int> dict;

        public char[] ByteEncoder { get; }

        public IEnumerable<NativeString> Keys => dict.Keys;

        public IEnumerable<int> Values => dict.Values;

        public int Count => dict.Count;

        public int this[NativeString key] => dict[key];

        public Vocab(int length, int capacity) {
            ptr = (char*)Marshal.AllocHGlobal(length * sizeof(char));
            offset = 0;
            this.length = length;
            dict = new Dictionary<NativeString, int>(capacity);
            ByteEncoder = new char[256];

            Init();
        }

        private void Init() {
            char[] table = BytesToUnicode();
            table.CopyTo((Span<char>)ByteEncoder);
            Array.Sort(table);

            foreach (char c in table) {
                ptr[offset] = c;
                dict.Add(new NativeString(ptr + offset, 1), dict.Count);
                offset++;
            }
        }

        public void Add(in (NativeString, NativeString) pair) {
            int len1 = pair.Item1.Length;
            int len2 = pair.Item2.Length;
            var newKey = new NativeString(ptr + offset, len1 + len2);

            Debug.Assert(offset + newKey.Length <= length);

            pair.Item1.Span.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef<char>(ptr + offset), len1));
            offset += len1;
            pair.Item2.Span.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef<char>(ptr + offset), len2));
            offset += len2;

            dict.Add(newKey, dict.Count);
        }

        public bool ContainsKey(NativeString key) {
            return dict.ContainsKey(key);
        }

        public bool TryGetValue(NativeString key, [MaybeNullWhen(false)] out int value) {
            return dict.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<NativeString, int>> GetEnumerator() {
            return dict.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        private void Dispose(bool disposing) {
            Marshal.FreeHGlobal((IntPtr)ptr);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Vocab() {
            Dispose(false);
        }


        private static char[] BytesToUnicode() {
            char[] arr = GC.AllocateUninitializedArray<char>(256);
            ref char p = ref MemoryMarshal.GetArrayDataReference(arr);
            char n = (char)256;

            for (char c = (char)0; c < 256; c++) {
                if (IsChar(c)) {
                    Unsafe.Add(ref p, c) = c;
                } else {
                    Unsafe.Add(ref p, c) = n++;
                }
            }

            return arr;


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static bool IsChar(char c) {
                return c is >= '!' and <= '~'
                    or >= '¡' and <= '¬'
                    or >= '®' and <= 'ÿ';
            }
        }
    }
}
