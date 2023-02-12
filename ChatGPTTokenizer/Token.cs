using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatGPTTokenizer {
    public readonly struct Token {
        public readonly int Id;
        public readonly int Index;
        public readonly int Length;

        public Token(int id, int index, int length) {
            Id = id;
            Index = index;
            Length = length;
        }

        public override string ToString() => $"id: {Id}, Index: {Index}, Length: {Length}";
    }
}
