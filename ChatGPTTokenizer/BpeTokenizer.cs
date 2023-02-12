using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace ChatGPTTokenizer {
    [DebuggerDisplay("Count = {vocab.Count}")]
    public sealed partial class BpeTokenizer : IDisposable {
        private readonly IntPtr buffer;
        private readonly int length;
        private readonly Dictionary<(NativeString, NativeString), int> mergesDict;
        private readonly Vocab vocab;
        private readonly Dictionary<string, Token[]> cache = new();

        public BpeTokenizer(string mergesText) {
            buffer = Marshal.StringToHGlobalUni(mergesText);
            length = mergesText.Length;
            mergesDict = new Dictionary<(NativeString, NativeString), int>(55000);
            vocab = new Vocab(mergesText.Length + 256, 55000);

            Init();
        }

        private void Init() {
            foreach (var pair in GetLines(buffer, length).Skip(1).Select(GetPair)) {
                mergesDict.Add(pair, mergesDict.Count);
                vocab.Add(pair);
            }
        }

        [SkipLocalsInit]
        unsafe private Token[] Bpe(ReadOnlySpan<char> token, int index) {
            string? tokenString = null;
            bool useCache = CacheRegex().IsMatch(token);
            if (useCache) {
                tokenString = token.ToString();
            }

            if (useCache && cache.TryGetValue(tokenString!, out Token[]? result)) {
                return result;
            }

            Span<byte> utf8Bytes = stackalloc byte[token.Length * 3];
            var status = Utf8.FromUtf16(token, utf8Bytes, out _, out int bytesWritten);
            if (status == OperationStatus.Done) {
                utf8Bytes = utf8Bytes[..bytesWritten];
            } else {
                tokenString ??= token.ToString();
                utf8Bytes = Encoding.UTF8.GetBytes(tokenString);
            }

            char* buffer = stackalloc char[utf8Bytes.Length];
            ref char table = ref MemoryMarshal.GetArrayDataReference(vocab.ByteEncoder);

            for (int i = 0; i < utf8Bytes.Length; i++) {
                buffer[i] = Unsafe.Add(ref table, utf8Bytes[i]);
            }

            result = Bpe(buffer, utf8Bytes.Length, index);
            if (useCache) cache.Add(tokenString!, result);
            return result;
        }

        [SkipLocalsInit]
        unsafe private Token[] Bpe(char* token, int length, int index) {
            if (length == 1) {
                return new[] { new Token(id: vocab[new NativeString(token, 1)], index, length: 1) };
            }

            int arrayLength = length;
            short* next = stackalloc short[length];
            for (int i = 0; i < length; i++) {
                next[i] = (short)(i + 1);
            }

            while (arrayLength > 1) {
                short minIndex = -1;
                int minValue = int.MaxValue;
                short i0 = 0;
                short i1 = next[0];

                while (true) {
                    short i2 = next[i1];
                    var k1 = new NativeString(token + i0, i1 - i0);
                    var k2 = new NativeString(token + i1, i2 - i1);
                    if (!mergesDict.TryGetValue((k1, k2), out int value)) {
                        value = int.MaxValue;
                    }
                    if (value < minValue) {
                        minValue = value;
                        minIndex = i0;
                    }

                    if (i2 == length) break;
                    i0 = i1;
                    i1 = i2;
                }

                if (minIndex < 0) break;

                i0 = minIndex;
                i1 = next[i0];
                next[i0] = next[i1];
                arrayLength--;
            }

            var bpeTokens = GC.AllocateUninitializedArray<Token>(arrayLength);
            short offset = 0;

            for (int i = 0; i < bpeTokens.Length; i++) {
                int tokenLength = next[offset] - offset;
                bpeTokens[i] = new Token(id: vocab[new NativeString(token + offset, tokenLength)], index + offset, tokenLength);
                offset = next[offset];
            }

            return bpeTokens;
        }



        unsafe public Token[] Encode(string text) {
            var result = new List<Token>();
            foreach (Match m in WordRegex().Matches(text).Cast<Match>()) {
                result.AddRange(Bpe(m.ValueSpan, m.Index));
            }
            return result.ToArray();
        }


        private void Dispose(bool disposing) {
            Marshal.FreeHGlobal(buffer);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~BpeTokenizer() {
            Dispose(false);
        }

        unsafe static (NativeString, NativeString) GetPair(NativeString line) {
            int splitIndex = line.Span.IndexOf(' ');
            if (splitIndex < 0) throw new FormatException();
            return (new NativeString(line.Ptr, splitIndex), new NativeString(line.Ptr + splitIndex + 1, line.Length - splitIndex - 1));
        }

        static IEnumerable<NativeString> GetLines(IntPtr mergesPtr, int mergesLength) {
            while (mergesLength > 0) {
                var line = FindNewLine(mergesPtr, mergesLength, out int skipChars);
                yield return line;
                int lineLength = line.Length + skipChars;
                mergesPtr += lineLength * sizeof(char);
                mergesLength -= lineLength;
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            unsafe static NativeString FindNewLine(IntPtr ptr, int length, out int skipChars) {
                var p = (char*)ptr;
                for (int i = 0; i < length; i++) {
                    if (p[i] == '\n') {
                        skipChars = 1;
                        return new NativeString(ptr, i);
                    }
                    if (p[i] == '\r' && i + 1 < length && p[i + 1] == '\n') {
                        skipChars = 2;
                        return new NativeString(ptr, i);
                    }
                }

                skipChars = 0;
                return new NativeString(ptr, length);
            }
        }

        [GeneratedRegex(@"'s|'t|'re|'ve|'m|'ll|'d|\s?(?:[a-z]+|\d+|[^\sa-z\d]+)?", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase)]
        private static partial Regex WordRegex();
        [GeneratedRegex(@"^(?:'s|'t|'re|'ve|'m|'ll|'d|\s?(?:[a-z]{1,50}|\d{1,50}|[^\sa-z\d]{1,6})?)$", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase)]
        private static partial Regex CacheRegex();
    }
}
